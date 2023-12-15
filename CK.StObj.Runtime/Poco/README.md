# Poco Type System

## Oblivious Types
Oblivious types is a subset of all the IPocoType. Their goal is to expose a smaller set
than all the types to ease code generation. A code generator can always work with 
all the types but there are less Oblivious types and they can be enough for some processes.
Oblivious types can be seen as Canonical types.

The "Oblivious" name comes the C# Nullable Reference Type (NRT) world (see [here](https://learn.microsoft.com/en-us/dotnet/csharp/nullable-references#nullable-contexts)).
An oblivious context is when NRT is disabled: reference types are always nullable, this is how
the runtime works, regardless of any ? or !.

For a basic Value type, its oblivious type is itself: a nullable value type is a totally different
type that its regular non nullable type because a nullable value type is a `Nullable<T>` with
its `T` value and a boolean that states whether it is null or not.

For a Reference type, the oblivious type is its non nullable version. **Caution:** this reverts
the "runtime behavior". We choose this to avoid all the '?' in oblivious type names.

TODO: to be extended...

