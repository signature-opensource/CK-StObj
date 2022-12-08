# ImportCodeGenerator

>> [ExportCodeGenerator](../Export/README.md) should be read before this.

## Reading is not Writing

Reading is a bit more complicated than writing. Everything starts with a type name and the set of supported
type names is the same as the exported one. The reader functions are stored in a simple dictionary.
This static dictionary is built once when the StObjMap is loaded and will only be read (no concurrence issues).
 
These top-level reader functions returns an `object` and powers the static `ReadAny` root method that
reads the type name from the first cell of the required 2-cells array `["type",<value>]` and calls the
appropriate reader function.

These object reader functions instantiate a non null object:
```csharp
delegate object ObjectReader( ref Utf8JsonReader r, PocoJsonImportOptions options );
```
They are limited to instantiate oblivious types: the type `IList<(int A, int B)>` is out of their scope because this
type is not an exported type name (it is exported as a "L((int,int))" that is `List<(int,int)>`).

These top-level functions cannot always be used internally: Poco collection types often use non regular collection
types (a `IList<IUserInfo>` where `IUserInfo : IPoco` is implemented by a generated specialized `List<IUserInfo>`
that supports an extended covariance). Moreover, Poco fields can be `readonly`: they cannot be assigned to a new instance,
only can they be filled with their content.

This why Importers are often more complex. Exporters rely on the ObliviousType that erase "internal types" to do
its job whereas Importers have to deal with these internal types.

## Code Generation

The first step is to build an array of `CodeReader` for each exchangeable and non nullable types:

`delegate void CodeReader( ICodeWriter write, string variableName );`

These functions know how to read their corresponding type into a named variable from a `Utf8JsonReader r` 
and a `PocoJsonImportOptions options`

For a basic type (`int`), the generated code simply uses the `Utf8JsonReader` API:
- The function is: `(w,v) => w.Append( v ).Append( " = r.GetInt32(); r.Read();" )`
- This generates for instance: `i = r.GetInt32(); r.Read();`

For a Poco object, the reader function calls the `ReadJson` method that is generated on each Poco class implementation.
The object is already allocated, this reader function "fills" an existing instance. This supports an incomplete
read: missing Json properties keep their default values.

```csharp
static CodeReader GetPocoReader( IPocoType type )
{
    return ( w, v ) => w.Append( v ).Append( ".ReadJson( ref r, options );" );
}
```
This generates for instance: `o.ReadJson( ref r, options );`

For an abstract Poco, the reader function calls the `ReadAny` function and casts its result:

```csharp
static CodeReader GetAbstractPocoReader( IPocoType type )
{
    return ( w, v ) =>
    {
        w.Append( v ).Append( "=(" ).Append( type.CSharpName ).Append( ")CK.Poco.Exc.JsonGen.Importer.ReadAny( ref r, options );" );
    };
}
```
   
For records, the reader function calls a generated function dedicated to the record type. The function uses a `ref`
parameter: the value must be available before the call.

```csharp
static CodeReader GetRecordCodeReader( IPocoType type )
{
    return ( w, v ) => w.Append( "CK.Poco.Exc.JsonGen.Importer.Read_" )
                        .Append( type.Index )
                        .Append( "(ref r,ref " )
                        .Append( v ).Append( ",options);" );
}
```

For collections, this is a little bit different. To minimize the generated code size, filling an array, a list, set
or dictionary is done by common functions that take a item reader function as a parameter. All arrays for instance are
read by the `ReadArray` helper:
```csharp
internal delegate T TypedReader<T>( ref Utf8JsonReader r, PocoJsonImportOptions options );

internal static T[] ReadArray<T>( ref Utf8JsonReader r, TypedReader<T> itemReader, PocoJsonImportOptions options )
{
    var c = new List<T>();
    r.Read();
    while( r.TokenType != System.Text.Json.JsonTokenType.EndArray )
    {
        c.Add( itemReader( ref r, options ) );
    }
    r.Read();
    return c.ToArray();
}
```
And all Lists or Sets are filled by the common `FillListOrSet` helper:
```csharp
internal static void FillListOrSet<T>( ref System.Text.Json.Utf8JsonReader r, ICollection<T> c, TypedReader<T> itemReader, CK.Poco.Exc.Json.Import.PocoJsonImportOptions options )
{
    r.Read();
    while( r.TokenType != System.Text.Json.JsonTokenType.EndArray )
    {
        c.Add( itemReader( ref r, options ) );
    }
    r.Read();
}
```
For each registered collection type, the reader function calls `GetReadFunctionName` to obtain the name of a
function that is able to read an item type instance:
```csharp
CodeReader GetArrayCodeReader( ICollectionPocoType type )
{
    var readerFunction = GetReadFunctionName( type.ItemTypes[0] );
    return ( writer, v ) => writer.Append( v ).Append( "=CK.Poco.Exc.JsonGen.Importer.ReadArray(ref r," )
                                  .Append( readerFunction )
                                  .Append( ",options);" );
}

CodeReader GetListOrSetCodeReader( ICollectionPocoType type )
{
    var readerFunction = GetReadFunctionName( type.ItemTypes[0] );
    return ( writer, v ) => writer.Append( "CK.Poco.Exc.JsonGen.Importer.FillListOrSet(ref r," )
                                  .Append( v )
                                  .Append( "," )
                                  .Append( readerFunction )
                                  .Append( ",options);" );
}
```

The call to `GetReadFunctionName` triggers the creation of a dedicated function that does what is needed to deserialize
the corresponding type. The creation of the function, if it has not been already generated, can be done immediately
because a collection item types appear necessarily before the collection that uses it: its CodeReader is already
available.  

Based on these CodeReader available for each type, the fundamental `GenerateRead` method can generate any
read code for any type. The code generated by this function takes care of nullable/not nullable, value/reference
types and whether the variable name provided needs to be initialized (new Poco, new collection or initial default value
of a record):

`void GenerateRead( ICodeWriter writer, IPocoType t, string variableName, bool? requiresInit )`

Once all the CodeReader are available, we can:
- Generate the `ReadJson` method on all Poco.
- Generate the `void Read_XXX( ref r, ref T record, options)` methods.
- Generate the `ReadAny` method that reads the 2-cells array with the type name, finds the top-level ObjectReader 
  in the static dictionary and calls it.



