## ExportCodeGenerator

The code generation of the export is a 3 steps process. Export relies on the ObliviousTypes:
- All reference types may actually be null.
- Anonymous record (value tuples) are exported without their field names (as an array of values).
- For collections, this is somehow an optimization: only the code to write regular collection types (`List<T>`, 
  `HashSet<T>` and `Dictionary<T>`) need to be generated.

The set of exported type names (visible to the consumers) is the Oblivious types' name:
- Oblivious reference types have non null names.
  - IPoco use their full C# name or [ExternalName] attribute.
  - Collections use "T[]", "L(T)", "S(T)", "M(TKey,TValue)" and "O(T)" for dictionaries with a string key.
- Value types can be nullable.
  - Both "int" and "int?" are exported.
  - Anonymous record type is "(T1,T2,...)". Here also "(T1,T2,...)?" can appear.
  - Named record are like IPoco: they use their full C# name or [ExternalName] attribute.

Abstract IPoco and union types are exported as `object`.

## Step 1: A CodeWriter for each non nullable type.

The first step is to associate a "code writer" to each non nullable type:
```csharp
/// <summary>
/// The code writer delegate is in charge of generating the write code into a <see cref="System.Text.Json.Utf8JsonWriter"/>
/// named "w" and a PocoJsonWriteContext variable named "wCtx" from a variable.
/// </summary>
/// <param name="write">The code writer to uses.</param>
/// <param name="variableName">The variable name to write.</param>
delegate void CodeWriter( ICodeWriter write, string variableName );
```

This writer function generates rather simple code that is totally inlined or is a call to a more complex method
- For `public enum Code { ... }` generates `w.WriteNumberValue( ((int)v) );`. 
- For the named record `public record struct Thing( string Name, int Count );`, this function 
  generates `CK.Poco.Exc.JsonGen.Exporter.WriteJson_44( w, ref v, wCtx );` (that is a static method).
- For a Poco, this function generates a call to the `WriteJson` method: `((PocoJsonExportSupport.IWriter)v).WriteJson( w, wCtx );` 

Based on these writers, all types can be written:
- If the type is a nullable value type, we must handle the null potential value explicitly and emit `null` or the non null value.
  This is done with a trick that avoids intermediate struct copy by using [DangerousGetValueOrDefaultReference](https://learn.microsoft.com/en-us/dotnet/api/microsoft.toolkit.highperformance.extensions.nullableextensions.dangerousgetvalueordefaultreference?view=win-comm-toolkit-dotnet-7.0)
  from `Microsoft.Toolkit.HighPerformance`.
- If the type is a reference type, then it can always be null (ObliviousType): we also must check a null value to emit a `null`
  or call the writer.

The full code of this step is here: [RegisterWriters](ExportCodeGenerator.RegisterWriters.cs).

## Step 2: Generation of the method bodies.

The second step is to implement all the required more complex writer methods than direct one-liner.
The full code of this step is here: [GenerateWriteMethods](ExportCodeGenerator.GenerateWriteMethods.cs).

For the `Thing` record struct, it is:
```csharp
internal static void WriteJson_44( System.Text.Json.Utf8JsonWriter w, ref CK.Poco.Exc.Json.Tests.RecordTests.Thing v, CK.Poco.Exc.Json.PocoJsonWriteContext wCtx )
{
    w.WriteStartObject();
    w.WritePropertyName( wCtx.Options.UseCamelCase ? @"name" : @"Name" );
    if( v.Name == null ) w.WriteNullValue();
    else
    {
        w.WriteStringValue( v.Name );
    }
    w.WritePropertyName( wCtx.Options.UseCamelCase ? @"count" : @"Count" );
    w.WriteNumberValue( v.Count );
    w.WriteEndObject();
}
```
For the following IPoco `public interface IWithRecord : IPoco { ref Thing Hop { get; } }`, it is the
publicly available [PocoJsonExportSupport.IWriter.WriteJson](../../CK.Poco.Exc.Json/Export/PocoJsonExportSupport.cs#L44)
instance method that writes each fields (here we have only one): 
```csharp
public void WriteJson( System.Text.Json.Utf8JsonWriter w, CK.Poco.Exc.Json.PocoJsonWriteContext wCtx )
{
    w.WriteStartObject();
    w.WritePropertyName( wCtx.Options.UseCamelCase ? @"hop" : @"Hop" );
    CK.Poco.Exc.JsonGen.Exporter.Write_44( w, ref _v0, wCtx );
    w.WriteEndObject();
}
```
When the `Thing` property is nullable `IWithNullableRecord : IPoco { ref Thing? Hop { get; } }`, the method becomes:
```csharp
public void WriteJson( System.Text.Json.Utf8JsonWriter w, CK.Poco.Exc.Json.PocoJsonWriteContext wCtx )
{
    w.WriteStartObject();
    w.WritePropertyName( wCtx.Options.UseCamelCase ? @"hop" : @"Hop" );
    if( !_v0.HasValue ) w.WriteNullValue();
    else
    {
        CK.Poco.Exc.JsonGen.Exporter.Write_44( w, ref CommunityToolkit.HighPerformance.NullableExtensions.DangerousGetValueOrDefaultReference( ref _v0 ), wCtx );
    }
    w.WriteEndObject();
}
```

All writer methods for value types use a `ref` parameter for their input: no copies are made for value types.

> There's room for improvement here. Not all possible optimizations have been done (such as using precomputed
utf8 spans for field names instead of strings).

### Writing collections

Only regular collection types (`List<T>`, `HashSet<T>` and `Dictionary<T>`) need to be generated. Other types
are casted into the regular ones. 

## Step 3: Generation of the WriteAny method.

When types are known, the writers described above do their job. But when an `object` must be written, we must first
detect the runtime type of the object to select the appropriate writer. This applies to the `object` type but also
to any "polymorphic data": union types and abstract IPoco (writing a Cris `ICommand` is writing it's real IPoco command).
For this polymorphic type to be read back by consumers, its type must be conveyed. Instead of the `$type` property used
by most of the serialization libraries, we use a different approach: a 2-cells array with the `["type name", ...and its value...]
is written.

The core of the static `void WriteAny( Utf8JsonWriter w, object o, PocoJsonExportWriteContext wCtx )` method
is basically a big switch case on `o.GetType()` that routes any object to its registered Oblivious type. The
switch is broken into smaller pieces for better performance.

Some of the switch cases require the types to be sorted from specialization to generalization. This is done by
the small [ObliviousReferenceTypeSorter](../../CK.Poco.Exchange.Engine/ObliviousReferenceTypeSorter.cs) helper.

The full code of this step is here: [GenerateWriteAny](ExportCodeGenerator.GenerateWriteAny.cs).
