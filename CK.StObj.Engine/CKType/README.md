# CKType

Types are cached to avoid instantiating arrays and objects like CustomAttributes provided by reflection
more than once during a CKSetup. This also provides stateful attributes and "Engine Attributes"
that are Engine implemented attribute surrogates.

The [CKTypeKind](CKTypeKind.cs) captures the CKomposable types families that participates to the
CKSetup process.

When this enumeration is `None`, we have no information on the type except that it exists.

| Bit | Name       | Description                               |
|-----|------------|-------------------------------------------|
|1    | IsExcluded | The type is excluded by [ExcludeCKType] or by configuration from the CKSetup process. It exists but brings nothing to the table. Internal types are excluded.|
|2    | IsDefiner |  The type is "abstract": it transfers its kind to its specializations.|
|3    | IsSuperDefiner | The type is "super abstract": its specializations are Definers. |
|4    | IsRealObject | An interface or a class that is a "true" singleton. A real object models a concept that exists necessarily once. A typical example is a database or a table in a database.|                          
|4    | IsPrimaryPoco | A IPoco interface that defines a family of IPoco.|                          
|5    | IsAbstractPoco | A [CKTypeDefiner]IPoco.|
|6    | IsSecondaryPoco | A IPoco interface that extends a Primary Poco.|   
|8    | IsScopedService | The type is known to be a scoped DI service. Each Unit of Work is provided a unique instance.|
|9    | IsSingletonService | The type is known to be a singleton DI service. All Unit of Works (including concurrent ones) in the DI context will use the same instance.|
|7    | IsAutoService | The class or interface is an automatically managed DI service: its single final implementation is selected and its lifetime can be automatically computed. |
|10   | IsEndpointService | The type is a DI service available in some endpoint contexts. |
|11   | IsBackgroundService | The type is a DI service available in the background context. |
|12   | IsNeutralService | The type is a DI service available in all endpoints and the background contexts. |
|13   | IsAmbientService | A neutral (necessarily available everywhere) and scoped service that is automatically marshalled from endpoints to the background context|
|14   | IsMultipleService| An interface that is implemented by one or more RealObject or service class. Resolvable by the DI container thanks to `IEnumerable<>`|

The `CKTypeKind` enumeration is the first layer of isolation between the complexity and the versatility
of a type system like the .NET one (but others are similar) and the application-level CKomposable types.

## Definer & SuperDefiner
The [CKTypeDefiner] and [CKTypeSuperDefiner] attributes can be declared an a type to define an
abstraction, a "template" that applies to its specializations but not directly to itself.

This is similar to an abstract base class (the "Template Method Pattern") or to an open constructed type
or a generic type definition (read about [the subleties here](https://learn.microsoft.com/en-us/dotnet/api/system.type.isgenerictype?view=net-8.0&redirectedfrom=MSDN#remarks)).

## Excludability
Types can be logically excluded from the CKSetup process. This capability is important in real life
to handle edge cases like choosing between two competing implementations of the same service (brought
by two different packages).

Excluding a type can be done directly in the code by declaring a `[ExcludeCKType]` attribute on a
class or interface. This can solve the edge case of a code driven alternative implementation of an
automatic service that needs to be substituded in some places.

It is more common to exclude types through configuration either because they conflict or because
the final system doesn't need it. (The latter is an architecture smell that should be addressed.)

Semantics of the exclusion is complex.

First thing to say is that not everything can be excluded: Definer and SuperDefiner cannot be excluded.
They correspond to types that appear in the type structure of the code base and saying that they are
not here is almost a non sense: what is a `class Dog : Animal` where we pretend that `Animal`
doesn't exist?

Abstractions cannot be excluded but they can eventually be useless: no "concrete" types exist that
structurally depend on them.

Hopefully, not every dependency is as "structural" as a base type. All the "Use" dependencies can
be questionned.

Depending on how the code is written (sometimes in suble ways), a code base can work with "holes"
in it.
- A service can optionally depend on another service by using a nullable parameter
  that defaults to null in its construtor.
- A `[IsMultiple]` service can have no implementation: the `IEnumerable<>` will be empty.

Excluding a service is possible. However there are restrictions:
- There is no interest to exclude a `[IsMultiple]` interface. A IsMultiple interface is a kind
  of Definer as it is an interface that is structurally supported by any number of implementations.
  Can excluding it mean to exclude its "multiplicity" (ie. not allowing the `IEnumerable<>` to be resolved?
  This is non sense and works against the .NET "Conformant DI container" from wich any `IEnumerable<T>`
  can be resolved when `T` can be resolved. Excluding a `[IsMultiple]` interface is a configuration error.
- Excluding the root marker interfaces is also prohibited.

For the others (classes, interfaces, generic or not) we have a choice to make:
- We allow top-down propagation of exclusion:
  - Excluding an open constructed type or a generic type definition excludes any derived types.
  - Excluding an interface or abstract base class excludes all its specializations.
- We consider exclusion as a surgery operation and exclusion bubbles up: only leaves can be
  excluded (recursively if needed... but explicitely, leaf by leaf).

There is no fundamental differences beween the two (the final state of the system is the same)
but the former is easier to configure. Even if exclusion is and should always be surgery, there
is no need to make it harder than needed.

For Poco, this is a little bit different.

- A complete Poco family can be excluded by excluding the primary Poco type.
  - This is possible only if references to this type appear in collections and/or as nullable
    properties of other Poco.
  - But, at runtime, if a code path somewhere needs to create an instance of this Poco this
    will fail miserably.   
- A secondary Poco *may* be excluded.
  - Its properties won't be merged into the primary ones.
  - Just like for primaries, the runtime failure risk exists... but is even more important: by
    design a Poco is always its Primary and any of its Secondary types and breaking this rule
    has a lot of impacts.

We only allow Primary Poco types to be excluded. 





