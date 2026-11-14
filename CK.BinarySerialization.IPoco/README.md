# CK.BinarySerialization.IPoco

Two resolvers that teach the binary serializer to handle any `IPoco`. Nothing to implement on your side:
no marker interface, no attribute.

> ℹ️ Read [CK.BinarySerialization](https://github.com/signature-opensource/CK-BinarySerialization/blob/master/CK.BinarySerialization/README.md) first: these are
> `ISerializerResolver` / `IDeserializerResolver` implementations plugged into its shared contexts.

## You do not register them

Referencing the package is enough. `SharedBinarySerializerContext` loads the resolver reflectively, by
assembly-qualified name, and says why in the code:

```csharp
// Currently, the Sliced and Poco companion should be systematically registered, so let's do
// this rather awful trick.
static readonly ISerializerResolver? _rPoco = (ISerializerResolver?)GetInstance( "CK.BinarySerialization.PocoSerializerResolver, CK.BinarySerialization.IPoco" );
```

`GetInstance` reads the static `Instance` field through `Type.GetType( aqn, throwOnError: false )`, so an
absent assembly is simply a null resolver rather than an error. The comment above it weighs the
alternative - `RuntimeHelpers.RunModuleConstructor` on assembly load - and rejects it, noting that code
generation may be the better answer eventually.

What *is* required is a `PocoDirectory` in the context's services: both resolvers call
`Services.GetRequiredService<PocoDirectory>()`, which throws when it is missing. On the write side a type
must also be a class that the directory can `Find`.

That requirement is the whole setup, and it is one line per context:

```csharp
// The de/serializer contexts' services must contain the PocoDirectory.
var sC = new BinarySerializerContext( BinarySerializer.DefaultSharedContext, auto.Services );
var dC = new BinaryDeserializerContext( BinaryDeserializer.DefaultSharedContext, auto.Services );

var o = auto.Services.GetRequiredService<PocoDirectory>().Create<ISimple>( o => o.Thing = "Goodbye!" );
```

Pass those contexts to the serializer and the Poco round-trips like anything else - the comment above is
the test suite's own, at
[`SimpleTests`](../Tests/CK.BinarySerialization.IPoco.Tests/SimpleTests.cs), which also checks
`BinarySerializer.IdempotenceCheck`.

## The Poco is written as JSON

The driver name says it: `"IPocoJson"`. A Poco is not written field by field in binary - it is written
as a JSON payload, length-prefixed. The write path serializes into a recyclable stream, checks the
resulting size against a ceiling, writes that length with `WriteNonNegativeSmallInt32`, then copies the
payload span by span. Reading it back is the mirror image - length, then the payload into a pooled
buffer - but the read is *looped*, because a decompressing stream may hand back fewer bytes than asked
for
([partial byte reads in streams](https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/6.0/partial-byte-reads-in-streams)).
A read that yields nothing before the announced length raises an `EndOfStreamException` naming both
counts, rather than handing a truncated payload to the JSON reader.

**No type envelope.** The usual Poco JSON form is `["TypeName",{ ... }]`; `withType: false` drops it
because the type is supplied differently - `GetTypeToWrite` returns the factory's `PrimaryInterface`, so
what the binary layer records is the primary interface rather than the concrete generated class. The
comment is explicit about how far that gets you, and it is worth reading before relying on it:

> We don't write the `["TypeName",{ ... }]` envelope since we rely on the rewritten Type that is the
> primary interface: the type name is free to change and it the type is hooked and a new TargetType is
> set, this **MAY** work...

So renaming is not a promise: it needs a deserialization hook that sets a new `TargetType`, and even
then the comment hedges.

**A different export option.** `ToStringDefault` is used rather than `PocoJsonExportOptions.Default`, and
the comment gives three reasons: Pascal case, `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` (faster), and
*"more importantly the TypeFilterName is "AllSerializable" (whereas the `PocoJsonExportOptions.Default`
is "AllExchangeable")"*. The filter is the one the comment itself foregrounds.

There is a hard ceiling: a single instance that needs more than `int.MaxValue / 2` bytes raises an
`InvalidOperationException` naming the size it wanted.

`SerializationVersion` is `-1`, and it *is* written like any other driver's version. `-1` is the house
value for a driver that defines none: *"This can be -1 when no version is defined."*

## Why the drivers are cached per context

Both resolvers depend on a `PocoDirectory`, and that is what decides where the cache lives.

On the write side, `CacheLevel` is `SerializationDriverCacheLevel.Context`: *"Drivers are cached at the
`BinarySerializerContext` level because everything depends on the `PocoDirectory`."* The directory is
resolved from `context.Services`, so two contexts with two directories must not share a driver.

The read side takes a shortcut worth knowing about. `PocoDeserializerResolver` caches its drivers
statically for **the first `PocoDirectory` it ever sees**, on the reasoning that *"in practice there's
one and only one PocoDirectory in a process/domain"*. A second directory in the same process still
works: the resolver falls through, finds the factory and builds an uncached driver - it simply loses the
cache.

## Requires.

- `CK.BinarySerialization`, `CK.Poco.Exc.Json` for the JSON form, and `CK.StObj.Model` for
  `PocoDirectory` and `IPocoFactory`.
