# Implementation notes



## No Nullable Reference Type, only nullable Value Type

Allowed types are handled by a dictionary of Type to [IJsonCodeGenHandler](IJsonCodeGenHandler.cs).
The whole JSON de/serialization support is based on .net types, not on [NullableTypeTree](https://github.com/Invenietis/CK-CodeGen/blob/master/CK.CodeGen/NullableType/NullableTypeTree.cs):
only nullable ValueType needs to be handled since the actual read and write C# code differ. Nullable reference type information (provided by the `NullableTypeTree`) are ignored:
reference types, from the JSON point of view, are always nullable (references, once read, must be validated against Nullable Reference Types). 

A `List<IUserInfo?>` is the same as a `List<IUserInfo>` and the Client side will never see a `IUserInfo?` but always a  `IUserInfo`.

On the contrary, nullable value types are seen by the Client since reading a `List<int>` or a `List<int?>` cannot be handled by the same code.
This distinction applies to the "ECMAScriptStandard" mode: a Client will receive `Number` and `Number?` and must send either a `BigInt` or a `BigInt?' type
when ambiguities exist.

## When Type Information is required

Type information uses a 2-cells array that contains the type name and the value (see [Structural Polymorphism](../../../CK.Poco.Json/README.md#structural_polymorphism)).
Everything is done to pay the cost of this overload only when necessary: when an ambiguity exists between at least 2 two different types.

## API for downstream libraries

Other libraries that interact with JSON need to have access to the JSON types, names and the choices that have been made.
Relevant informations are available on the [JsonSerializationCodeGen](JsonSerializationCodeGen.cs) service that is exposed
as a service to other CKSetup participants. This service enables new types to be registered with their
[CodeReader and CodeWriter](CodeReaderWriterDelegates.cs) and finds or tries to create a [IJsonCodeGenHandler](IJsonCodeGenHandler.cs)
for a type.

Moreover, Json information is available on each [IPocoRootInfo](../IPocoRootInfo.cs) as an annotation.
The [IPocoJsonInfo](IPocoJsonInfo.cs) and its [IPocoJsonPropertyInfo](IPocoJsonPropertyInfo.cs) properties can be
easily retrieved:
 
````csharp
void DoSometing( IPocoRootInfo poco )
{
    var jsonInfo = poco.Annotation<IPocoJsonInfo>();
    if( jsonInfo == null )
    {
        // An error occurred.
        // The setup has not yet failed (otherwise we won't be here) but it will.
    }
    else
    {
        // Uses the Json information to generate TypeScript code for instance...
    }
}
```` 


### About number handling

The [JsonNumberHandling](https://docs.microsoft.com/en-us/dotnet/api/system.text.json.serialization.jsonnumberhandling) is NOT available on the Utf8JsonWriter API.
It is handled by the "Serialization" layer. And the APIs to [write (WriteNumberValueAsString(ulong))](https://source.dot.net/#System.Text.Json/System/Text/Json/Writer/Utf8JsonWriter.WriteValues.UnsignedNumber.cs,112)
or [read (GetUInt64WithQuotes)](https://source.dot.net/#System.Text.Json/System/Text/Json/Reader/Utf8JsonReader.TryGet.cs,383) quoted long or unsigned long (same for Decimal) are not publicly
exposed on the Utf8Writer/Reader.

> We currently handle long, ulong, decimal, BigInteger and TimeSpan (BigInteger and TimeSpan have no direct support) by writing/reading and parsing strings.
> Waiting for https://github.com/dotnet/runtime/issues/54016 and https://github.com/dotnet/runtime/issues/1784 (for BigInteger) for a more efficient implementation.

