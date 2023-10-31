# Poco Exchange

This package defines abstractions related to serialization and deserialization of Poco. Poco are the **roots of serialization**,
they bootstrap the serialization and deserialization process.

## Serializers and Deserializers
Serialization and/or deserialization support are typically implemented in independent packages (the "Package First" approach)
but of course nothing prevents a direct implementation in a specific solution.

The 2 following core interfaces define the fundamental behavior:
```csharp
public interface IPocoSerializer
{
    void Write( IActivityMonitor monitor, Stream output, IPoco? data );
    Task WriteAsync( IActivityMonitor monitor, Stream output, IPoco? data, CancellationToken cancel );
}

public interface IPocoDeserializer
{
    bool TryRead( IActivityMonitor monitor, Stream input, out IPoco? data );
    Task<(bool Success, IPoco? Data)> TryReadAsync( IActivityMonitor monitor, Stream input, CancellationToken cancel );
    IPoco? Read( IActivityMonitor monitor, Stream input );
    Task<IPoco?> ReadAsync( IActivityMonitor monitor, Stream input, CancellationToken cancel );
}
```
**Key points:**
- Synchronous and asynchronous implementations must be provided even if the async is a "fake" async on sync implementation 
  (recall that the other way around, sync on async, is not a good idea).
- A null Poco can always be read or written: `null` is a valid data. If it has to be considered invalid, this is up to the callers 
  to handle this.
- Read can more often fail than Write: this is why implementing a deserializer requires to implement the 2 versions.
  Depending on the underlying code (whether it throws or implements error management without exceptions), the methods must
  adapt its behavior.

## Importers and Exporters: the ProtocolName
Extending these 2 interfaces, importers and exporters are singleton (multiple) auto services:

```csharp
[IsMultiple]
public interface IPocoExporter : IPocoSerializer, ISingletonAutoService
{
   string ProtocolName { get; }
}

[IsMultiple]
public interface IPocoImporter : IPocoDeserializer, ISingletonAutoService
{
   string ProtocolName { get; }
}
```

They can be implemented by 2 different services (that may even be in 2 different packages!) or be both
implemented by the same class.

**Key points:**
- `ProtocolName` typically denotes a "ContentType" (IANA media types) but may denote a more complex protocol.
- There is no Options nor Context of any kind: the `ProtocolName` fully describes what and how things are serialized and deserialized.

Forcing these implementations to be singletons somehow extends the idea that "There is No Options": imports
and exports should always be as contextless as possible.

The [`PocoExchangeService`](PocoExchangeService.cs) collects the available importers/exporters that can be resolved by
their `ProtocolName`.

## Contextual Importers and Exporters: the Factories.

Nothing prevents contextual serializes/deserializers to be implemented with more complex protocols. Specific
stores of serializes/deserializers are bound to the target/receiver "Party" of the exchanged data, for instance: 
- A protocol that supports a kind of Dictionary-based compression.
- A protocol that can benefit or requires the knowledge of the model of the IPoco types before deserializing (optimized binary 
  protocol).
- A protocol that can adapt its behavior based on a "versioning schema" and is able serialize/deserialize based on "previous model"
  of the data. 

All these crazy things can be implemented by managing a store by "Party" that can memorize and use the required knowledge
(and is able to talk to the Party).

> Meaning that this is doable doesn't mean that this is done :).

Any such contextualization nevertheless fit into the general model thanks to [`IPocoImporterFactory`](IPocoImporterFactory.cs)
and [`IPocoExporterFactory`](IPocoExporterFactory.cs). They rely on the [`PocoExtendedProtocolName`](PocoExtendedProtocolName.cs)
Thanks to this, the `PocoExchangeService` is able to resolve "dynamic", contextual, importers and exporters.

## Code Generated pattern

Protocols can be implemented by generated code (following the "Package First" approach). When it's the case,
the implementation should follow the following pattern:

The first step it to give serialization/deserialization capabilities to IPoco itself through extension methods.
By installing a CK.Poco.Exc.XXX package in a project, at least the 3 following extension methods appear:
- `IPoco?.WriteXXX( ... )` (Writing a null IPoco must always be possible.)
- `T? IPocoFactory<T>.ReadXXX<T>( ... )`
- `IPoco? PocoDirectory.ReadXXX( ... )`

Note that it is possible to split this into 2 packages:
- CK.Poco.Exc.XXX.Export will only bring the Write, Serialize, Export capabilities.
- CK.Poco.Exc.XXX.Import will only bring the Read, Deserialize, Import capabilities.

Actual parameters of these methods are Protocol dependent. `CK.Poco.Exc.Json` for instance, exposes:
- `IPoco.WriteJson( Utf8JsonWriter writer, bool withType = true, PocoJsonExportOptions? options = null )`
- `T? IPocoFactory<T>.ReadJson<T>( ref Utf8JsonReader reader, PocoJsonImportOptions? options = null )`
- `IPoco? PocoDirectory.ReadJson( ref Utf8JsonReader reader, PocoJsonImportOptions? options = null )`

This package offers more extension methods (helpers) to ease the use of the API like reading from a `ReadOnlySpan<char>`,
a `string`, etc., but these 3 methods are the core of the Exchange specification.

This enables a lot of optimizations and versatility since any kind of parameters can be used.
For instance the CK.Poco.Exc.Json above is basically synchronous, no Read/WriteAsync are supported (just because
this is not easy to support), but it CAN always be done.

The second step is to implement one or more `IPocoImporter` and `IPocoExporter` that are basically
adapters from `Stream` to whatever is needed to serialize/deserialize the IPoco types.
Their `ProtocolName` embeds/defines/summarizes/describes the "options" used to serialize/deserialize: a package can
expose multiple `IPocoImporter` and `IPocoExporter` that embed for each protocol name the options that will be used.




