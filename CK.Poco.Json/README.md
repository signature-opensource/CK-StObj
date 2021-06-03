# Json serialization

This package triggers the code generation of Json serialization and deserialization of Poco. Poco are the roots of serialization,
they bootstrap the serialization and deserialization process.

Even if other types can be supported, serialized data must be subordinated to a Poco (it must appear below a Poco property).

The way this work is more complex that the basics System.Text.Json since it is not a converter (see [here](https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-converters-how-to)
for instance) that must be written but the code that generates the code to read/write objects. More complex but also deeply "type safe" and
potentially more efficient.

## Basic types

Serialization uses the [Utf8JsonWriter](https://docs.microsoft.com/en-us/dotnet/api/system.text.json.utf8jsonwriter) and deserialization uses
the [Utf8JsonReader](https://docs.microsoft.com/en-us/dotnet/api/system.text.json.utf8jsonreader) with the default options.

The following basic types are handled: bool, string, int, long, ulong, uint, double, float, Decimal, byte, sbyte, short, ushort, DateTime, DateTimeOffset, TimeSpan,
byte[] (encoded in base64: https://source.dot.net/#System.Text.Json/System/Text/Json/Writer/Utf8JsonWriter.WriteValues.Bytes.cs) and Guid.

The following complex types are handled:

- `T[]` (array), `IList<T>`, `List<T>`, `ISet<T>`, `Set<T>` are serialized as arrays (they can be interchanged). 
- `IDictionary<,>` and `Dictionary<,>` are serialized as Json objects when the type of the key is string and arrays of 2-cells arrays when the key is an object. 
- Value tuples are serialized as arrays.

Nullable value types and nullable reference types are automatically handled.

Enum values are serialized with their numerical values.

## Registered types only

Only registered types can be de/serialized (typical ). Even enums must be registered to be serializable. Knowing the complete set
of the serializable types can be complex. This is where the Poco help: as soon as a Poco must be serializable, the transitive
closure of the referenced types (the Poco's properties' types) are automatically registered.

> Only types that are really needed are registered (and the de/serialization code is generated). Code to handle what may seem basic types like `int?` 
> or `string[]` will be generated only if they are actually used by at least one Poco.

A Poco property can be an untyped `object` (may be defined by a [UnionType](../CK.StObj.Model/Poco/UnionTypeAttribute.cs)), a value tuple, list, array, etc.
When de/serializing an `object` or any abstraction (an interface or a base class), a 2-cells array with the type name and the value is used.


