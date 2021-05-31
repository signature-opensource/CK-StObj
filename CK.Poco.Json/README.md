# Json serialization

This package triggers the code generation of Json serialization and deserialization of Poco. Poco are the roots of serialization,
they bootstrap the serialization and deserialization process.

Even if other types can be supported, serialized data must be subordinated to a Poco (it must appear below a Poco property).

The way this work is more complex that the basics System.Text.Json since it is not a converter (see [here](https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-converters-how-to)
for instance) that must be written but the code that generates the code to read/write objects. More complex but also deeply "type safe" and
potentially more efficient.

## Basic types

By default, the following types are handled:


| C#       |  Json type    |  Remarks |
|----------|:-------------:|------|
| int      |  number       | An integer is directly mapped to the javascript number (double). |
| string   |  string       | Direct mapping. |
| bool     |  boolean      | Direct mapping. |


A Poco property can be an untyped `object` (may be defined by a [UnionType](../CK.StObj.Model/Poco/UnionTypeAttribute.cs)), a value tuple
