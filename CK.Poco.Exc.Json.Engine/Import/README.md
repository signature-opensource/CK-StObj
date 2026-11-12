# ImportCodeGenerator

Generates the `Importer` static class (namespace `CK.Poco.Exc.JsonGen`) and the `ReadJson` methods of the Pocos.

## The numbers

JSON has a single number type: `1`, `1.0` and `1e0` are the same value. An integral type (`int`, `byte`, `long`...,
an enum, the ticks of a `TimeSpan`) reads the plain integer token directly, and accepts any other number whose value is
integral and in the range of the type (`Importer.ReadInt32`, `Importer.ReadByte`...): `1.5`, or `256` for a `byte`, is a
`JsonException`. `long`, `ulong` and `decimal` also accept a string (`"3712"`), the form that the export writes.
