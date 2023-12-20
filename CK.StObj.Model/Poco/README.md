# Poco

Poco are contracts, interfaces that extends the [`IPoco`](IPoco.cs) interface.
Any interface that directly extends [`IPoco`](IPoco.cs) defines a "family" of types that will eventually be implemented
by an automatically generated concrete class that will support all the interfaces of the family: among a Poco family,
IPoco interfaces are BOTH co and contravariant.

The Poco framework has two primary goals:

- Enables the definition and use of truly modular and extensible types across packages.
- Be the "Lingua franca" of importable and exportable data that enter and leave a System.

## Modularity and extensibility: IPoco families

IPoco aims to define truly modular types. Across independent packages, a common type (from a base package)
can be freely extended to carry specific, specialized information without the others having to bother
about those new properties.

### Generic IPoco

A IPoco family cannot be defined by a generic interface. If this was possible different extensions could use
different types for the same type parameter.

This is forbidden:

```csharp
public interface IAmAmbiguous<T> : IPoco
{
    T Value { get; set; }
}

public interface IWantAnInt : IAmAmbiguous<int>
{
}

public interface IWantAnObject : IAmAmbiguous<object>
{
}
```

#### The [CKTypeDefiner] defines IPoco family
Using the `[CKTypeDefiner]` attribute enables a generic definition of a "family of family". This is how 
Commands and their results are modeled by CRIS:

```csharp
/// <summary>
/// The base command interface marker is a simple <see cref="IPoco"/>.
/// Any type that extends this interface defines a new command type.
/// </summary>
[CKTypeDefiner]
public interface ICommand : IPoco
{
    /// <summary>
    /// Gets the <see cref="ICommandModel"/> that describes this command.
    /// This property is automatically implemented. 
    /// </summary>
    [AutoImplementationClaim]
    ICommandModel CommandModel { get; }
}

/// <summary>
/// Describes a type of command that expects a result.
/// </summary>
/// <typeparam name="TResult">Type of the expected result.</typeparam>
[CKTypeDefiner]
public interface ICommand<out TResult> : ICommand
{
}
```
By using a `[CKTypeDefiner]` attribute on a `IPoco`, the interface becomes a kind of "abstract" definition.
The definer is NOT a `IPoco`, doesn't define a "Poco family", only the interfaces that specialize it are `IPoco`
and define a family.

> `[CKTypeDefiner]` attribute is the **abstract** of the Poco Type System.

__Notes:__
- `[AutoImplementationClaim]` is an "advanced" attribute that states that this member is not a
regular property, it will be automatically implemented by some aspects of the framework.
- The real Cris command model is a bit more complex than that.

#### The [CKTypeSuperDefiner] defines abstract IPoco
Using the `[CKTypeSuperDefiner]` makes direct extensions on the interface an abstraction rather
than a family, as if they all have the `[CKTypeDefiner]` attribute. It may be easier to consider
them as definitions of "parts" or "mixin" for IPoco.

This is also used by CRIS to model common, reusable parts of commands:

```csharp
/// <summary>
/// Marker interface to define mixable command parts.
/// </summary>
/// <remarks>
/// Parts can be composed: when defining a specialized part that extends an
/// existing <see cref="ICommandPart"/>, the <see cref="CKTypeDefinerAttribute"/> must be
/// applied to the specialized part.
/// </remarks>
[CKTypeSuperDefiner]
public interface ICommandPart : ICommand
{
}

// This is a command part ([CKTypeDefiner] attribute is implied - and useless).
public interface ICommandAuthUnsafe : ICommandPart
{
    int ActorId { get; set; }
}
```

### The IClosedPoco

The [`IClosedPoco`](IClosedPoco.cs) interface marker (no properties) is a `[CKTypeDefiner]` that
expects the final IPoco to be "closed": one of its IPoco definition MUST expose all the properties
of all its interfaces.

A Poco family can have such a "Closure interface" without being a `IClosedPoco`, but if `IClosedPoco` belongs
to the family definition, this closure is guarenteed to exist: when no closure can be found, it is an error.

## Poco for Data Exchange

The multiple `IPoco` interfaces that appear in a runnable project define a closed set of public types that
should be the only ones that can be exchanged with the external world: this set of types should be the
"external data API" of the System.

Being able to control the types that a given System can exchange (export, import, serialize) with the
external world is important in terms of security and maintainability, but there is a much more ambitious
goal in this approach: freeing the developer to cope with data exchange protocols once for all...

### The standard approach
The classical approach to data I/O is that a System implements serializers/deserializers that implement
a given protocol, or adapts its types to protocols by mapping its types from/to intermediate objects provided
by a library that manages the de/serialization, etc.

This classical approach has the following drawbacks:
- Specific code (at the application level, where Types are known) has to be written for each supported protocol.
- Protocols capabilities differ greatly: some constructs or basic types may be missing (no Decimal support in ProtoBuf
for instance). How the mapping should be done (how to transmit a Decimal) requires a "super protocol" to actually be defined
and implemented.
- An may be the worst of all the impacts: sometimes the required I/O protocol impacts the application Type definition itself: 
the I/O constraints leak in the application code. For example, as there are no natural support of Tuples with ProtoBuf (tuples
must be expressed explicitly with messages composed of fields `Item1`, `Item2`, etc.): the lead developer has prohibited the
use of tuples.

The Poco approach is to consider that the Application relies on IPoco to model any data that may need to be exchanged and then
one or more "once-for-all written protocol support" are used.

### The Poco approach

How can this be a solution? Is it even doable?

On the protocol side, there is no silver bullet here.
- Standard protocols support have to be written cleverly. 
  - Any "Super Protocol" used must be clearly documented (how a Decimal is exchanged, the fact that `_Item1`, `_Item2`... 
  are the suffixes for tuple values of tuple fields, etc.)
  - Totally unsupported types by a given Protocol should be known and ideally detected early (at setup time).
- A "Deviant" protocol (for a partner system that requires json object's properties to be lexicographically sorted - yes that exists...) 
should be as easy as possible to implement and use.

On the data side, we must be confident that the Poco model IS a superset of what should ever be needed (or be confident that if a
type or a construct is missing, it could be easily added).

- We work in C#: we know our language capabilities, the basic types we need. We can support them once for all.
- We believe that the "Poco compliant types" we support are enough to model any "exchange data" notably with the
support of the support of a union type (the `oneof`) for IPoco property.

## This is an ongoing work...

One of the goal is to automatically support ReadOnly and Immutable poco and this far esaier to say than to
achieve.

An idealized view of ReadOnly and Immutable could be to consider that the ReadOnly aspect is a view on
any mutable Poco compliant type and that Immutability is simply the same ReadOnly aspect on a snapshot
of the mutable type. This "vision" is the target but I'm not sure this is can be done, especially if we
want to fully support covariance (that we should).

Current implementation has no ReadOnly nor Immutable support simply because it is harder than I initially
envisioned but there are restrictions and checks that should help supporting them in the future.

## Poco compliant types

The set of Poco compliant type is precisely defined:

- Basic types:
  - Value types: `int`, `long`, `short`, `byte`, `bool`, `double`, `float`, `object`, `DateTime`, `DateTimeOffset`,
    `TimeSpan`, `Guid`, `decimal`, `System.Numerics.BigInteger`, `uint`, `ulong`, `ushort`, `sbyte` and
    `SimpleUserMessage`, `UserMessage` and `FormattedString`.
  - Reference types: `string`, `ExtendedCultureInfo`, `NormalizedCultureInfo`, `MCString` and `CodeString`
  - Formally `object` is a basic type provided that at runtime, the instance must be a compliant type.
- `IPoco` objects that can be:
  - Primary: this is the interface that denotes a family (`IUserInfo : IPoco`).
  - Secondary: an interface that extends a family (`IColoredUserInfo : IUserInfo`).
  - Abstract: an interface that is marked with `[CKTypeDefiner]` can appear in multiple families (like `ICommand` or
    the base `IPoco`).
- Value tuples (or fully mutable structs) of compliant Poco types.
- Collections can be `List<>`, `IList<>`, `HashSet<>`, `ISet<>`, `Dictionary<,>` `IDictionary<,>` and
  array of Poco compliant type.


### The PocoRecord

A PocoRecord is a mutable struct. It aims to capture "micro local types". They are detailed below.

#### ValueTuple: the "Anonymous Record"

A value tuple is like an anonymous type that locally defines a small structure of basic types. The following
class property seems fine:
```csharp
class AmIWrong
{
    public (int Power, string Name) Simple { get; set; }
}
```
However, there's something wrong here about nullability. Despites the non nullable string and list, `Name` and `Values` are
null by default and absolutely no warning of any kind will help the developer see this.
Another aspect that can be surprising is that a ValueTuple is a... value type. Its individual fields cannot be set independently:
the whole tuple has to be reassigned to a new one. Care must be taken to copy the "previous" other field values from the original
one. To solve this value tuples and mutable structs must be `ref` properties in a `IPoco`:
```csharp
public interface IWithValueTuple : IPoco
{
    ref (int Power, string Name) Thing { get; }
}
```
Initial values are guaranteed to follow the nullability rules (here Name will the empty string and
initial `Power` field will be 0).

> The `ref` enables the individual fields to be set and the tuple then becomes a "local" sub type, an
"anonymous record". 

Value tuples can be nested:
```csharp
public interface IOneInside : IPoco
{
    ref (int A, (string Name, int Power) B) Thing { get; }
}
```
Here again, the non nullable `Thing.B.Name` will be the empty string.
Nesting is allowed... But:
- It becomes quickly hard to read.
- Fields are not "by ref"! It can be tedious to copy the whole subordinated
  structure to update a value in a value.

For more information on value tuple and more specifically their field names, please read this excellent
analysis: http://mustoverride.com/tuples_names/. The Poco framework handles the field names so that
they can be exploited by de/serializers and importers/exporters if needed.

#### Mutable structs: the "Record"
The `record struct` introduced in C#10 used with the positional parameters syntax is quite isomorph to a
ValueTuple since they are mutable value types with 2 added benefits:
- the default parameter values naturally express the field default value;
- being an explicitly named type it acts as reusable definition of its set of fields.

Record struct have:
 - Generated value equality and this is important.
 - ToString method (but we don't really care here)
 - A syntactic sugar for the constructor with positional parameters enables to easily describe the
   data structure and the default values.

The previous example can easily be rewritten with a reused `record struct` for 2 properties:
```csharp
public interface IWithRecordStruct : IPoco
{
    public record struct ThingDetail( int Power, string Name = "Albert" );

    ref ThingDetail Thing1 { get; }
    ref ThingDetail Thing2 { get; }
}
```
Note that even if `record struct` and value tuples are equivalent at this level, they are different. There
are no field aliases `Item1`, `Item2`,... available in record struct, only the property names matter (this is
somehow the reverse as how the Value Tuples work).

A word about the `record class`: it is useless for us here since it is a reference type and immutable by default.
To have it mutable, the standard syntax must be used: there is no real difference between defining a `IPoco` and
a `record class` (and also no difference in the final implementation: it's a class with its backing fields):
```csharp
public interface IPerson : IPoco
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
}
public record Person
{
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
}
```
Note the `default!` that explicitly breaks the "not nullable" contracts to avoid warnings... In the IPoco,
since there is no `[DefaultValue]`, the empty string is used by default.

#### IPoco fields can only be IsReadOnlyCompliant records
A record that doesn't contain any mutable reference type is "read only compliant": a copy of the value
is de facto a "readonly" projection of its source in the sense where it cannot be used to mutate the
source data. 

Only "read only compliant" records can be IPoco field (this restriction is here to ease support of
ReadOnly poco). Such record cannot contain field thar are `IPoco` or collection.

#### Fully mutable structs are "Record" IF they implement their IEquatable 
Value Tuples and `record struct` are finally the same for the Poco framework: they are value types that
share the same restrictions:
- Must be fully mutable.
- Must contain only fields of compliant Poco types.
- Must implement `IEquatable<TSelf>`.
- When used as a field in a IPoco, it must be exposed by a `ref` property AND must be "read only compliant".

These are valid Poco record definitions (`ThingDetail` is "read only compliant, but `DetailWithFields` is not):
```csharp
public record struct ThingDetail( int Power, string Name = "Albert" );

public struct DetailWithFields : IEquatable<DetailWithFields>
{
    [DefaultValue( 42 )]
    public int Power;

    public List<int>? Values;

    [DefaultValue( "Hip!" )]
    public string? Name;

    public readonly bool Equals( DetailWithFields other ) => Power == other.Power
                                                              && Name == other.Name
                                                              && EqualityComparer<List<int>>.Default.Equals( Values, other.Values );

    public override bool Equals( [NotNullWhen( true )] object? obj ) => obj is DetailWithFields other && Equals( other );

    public override int GetHashCode()
    {
        return HashCode.Combine( Power, Name, EqualityComparer<List<int>>.Default.GetHashCode( Values ) );
    }
}
```

### The conformant collections

IPoco can expose `T[]`, `IList<T>`, `ISet<T>` and `IDictionary<TKey,TValue>` where:
- `T`, `TKey` and `TValue` must be a Basic type, a IPoco (abstract or not) or a record (anonymous or not) of Basic types.
- `TKey` is not nullable.

Recursive collections are forbidden except for arrays. One cannot define a `IList<IList<int>>` or a `ISet<object[]>`
but `int[][]` is valid.

### IPoco properties

A IPoco property can be:
- writable `{get; set;}` or `ref ... {get;}` for records
- or read only `{get;}` 
- and can be nullable or not.

Whether they are writable or read only, a non nullable property type requires an initial non null value
(otherwise the newly created Poco will not be valid): the code generator takes care of this by generating
a default constructor that setups the fields, including nested records' fields.

#### Non nullable properties: the initial value

Nullable properties are easy: their initial value is always the `default` for
the type and that is null for reference and value types.

Non nullable properties MUST have an initial value. For value type, this is not an issue since here again
the `default` for the type is the "natural", expected initial value.

When a property type is a **not nullable reference type**, then the constructor MUST
be able to assign a new instance to it, otherwise the newly created Poco will not be valid.

The only true basic type that is a reference type is the `string`, the
[`[DefaultValue]`](https://learn.microsoft.com/en-us/dotnet/api/system.componentmodel.defaultvalueattribute)
attribute must then be used to provide a default value. The empty string being a quite natural "default" for a
non nullable string, if no `[DefaultValue]` exists, the empty string is automatically used.
The other basic type that is a reference type is the `object`: if it's not an "Abstract Read Only Property" (see below)
there MUST be a `[DefaultValue]` attribute defined for all non nullable `object` properties.

For any other non nullable property types (records, collection, IPoco) the constructor must be
able to synthesize a default instance.
- For `IPoco`, it must be a "concrete" interface, not an "abstract" one (that is a `[CKTypeDefiner]`).
- For collections (`IList<>`, `List<>`, `IHashSet<>`, `HashSet<>`, `IDictionary<,>` `Dictionary<,>` and arrays), the 
  constructor creates an empty instance of the type (for arrays, the `Array.Empty<T>()` singleton is used). 
- For record struct (with positional constructor syntax), the default is the default value parameter or a default
  must be resolved.
- Value tuples are like record struct with no default values expressed (no `[DefaultValue]` attribute): we must 
  be able find an initial value for all non nullable fields of the tuple .

#### Writable vs. Read only properties: the "Abstract Read Only Properties"
A read only property can appear on one or more IPoco interface of a family and also exists
in a writable form on other interfaces. When this happens, the "writable wins": the Poco's
property is actually writable. The read only definitions become "hidden" by the writable ones
and we call them "Abstract Read Only Properties".

**All writable definitions of the same property must be exactly the same**: they are strictly type invariants
(including nullability).

A read only property that has a writable definition somewhere in the Poco family becomes "Abstract": its type
doesn't need to be exactly the same as the writable type anymore, it can be any type that is "assignable from",
"compliant with" the writable one.

This concept may seem overkill (and it somehow is) but the goal is to exploit the `IPoco` capacity: a `[CKTypeDefiner]`
attribute on a `IPoco` defines a kind of "abstract" definition. Such definer SHOULD be able to carry "abstract projections"
by exposing read only properties:
  - An `object Identifier { get; }` that can be implemented (at the "concrete" `IPoco` level) by a 
    `int Identifier { get ; set; }` for a family and by `string Identifier { get; set; }` for another one.
  - A `IReadOnlyList<X> Things { get; }` that can be implemented by a `IList<Y> Things { get; }` where X 
    is assignable from Y.

> Abstract Read Only Properties are the the basis of the future ReadOnly Poco support. ReadOnly and
Immutable will rely on this to "allow as much covariance" as possible.

This is all about covariance of the model (the latter example relies on the `IReadOnlyList<out T>` **out**
specification). This relation between 2 types is written `<:`. Below is listed some **expected covariances** and the (sad)
reality about them:

| Example  | .Net support | Remarks |
|---|---|---|
| `object`&nbsp;<:&nbsp;`IUser` | Yes  | Basic "object paradigm": everything is object.
| `object` <: `int` | Yes  | Thanks to boxing, the  "object paradigm" applies.
| `int?` <: `int` | Yes  | This is handled by the .Net runtime. |
| `IReadOnlyList<object>`&nbsp;<:&nbsp;`List<IUser>`   |  Yes | Thanks to the **out** generic parameter. |
| `IReadOnlyList<object>` <: `List<int>`   |  No | Unfortunately, boxing is not supported "out of the box"... |
| `IReadOnlyList<object>` <: `IList<T>`  | No | The read only interface is not defined on the writable one. This also applies to `ISet<>` and `IDictionary<,>`  |
| `IReadOnlyList<T?>` <: `IList<T>`  | Maybe | T must be a reference type for this to be possible... And this works because Reference Type Nullability is only based on attributes. For the runtime, every `object` is a `object?`. |
| `IReadOnlySet<object>` <: `HashSet<IUser>`  | No | Read only HashSet and Dictionaries are not covariant. |
| `IReadOnlyDictionary<int,object>` <: `Dictionary<int,IUser>`  | No | Read only HashSet and Dictionaries are not covariant. |

To overcome these limitations and unifies the API, when `IList<>`, `ISet<>` or  `IDictionary<,>` are used to define Poco properties,
extended collection types are generated that automatically support all these expected covariance.

**The IPoco framework automatically corrects these cases**... but this comes with restrictions:
- This only applies to direct IPoco properties.
- Only abstract `IList<>`, `ISet<>` or `IDictionary<,>` collections must be used for Poco fields.
  It is an error to define a IPoco property by a concrete collection.
- Such collections cannot be a collection of collection: `IList<IList<int>>` is forbidden. 
- Dictionary key is invariant.

Outside of IPoco fields, only concrete `List<>`, `HashSet<>` and `Dictionary<,>` can be used and they can
be recursive: a `List<Dictionary<int,object>>` is valid.

Note that this "extended covariance":
- **Supports nullability** (and enforces it!): `IReadOnlyList<object?>` <: `IList<IUser>` is allowed and supported BUT 
  `IReadOnlyList<object>` <: `IList<IUser?>` is an error (even for reference types).
- Considers dictionary key as being invariant. (Even if this can be done, the number of required adaptations to support this would explode.)

> Currently, `IReadOnlyList/Set/Dictionary` are NOT YET supported (it is internally implemented but not exposed).

When all definitions of a property are read only (no writable appears in the family), this property is either:
  - Nullable... and it keeps its default null value forever. This is _stupid_. 
  - Not nullable... and it keeps its initial value forever. This seems _stupid_ for basic types but it is not for collections,
    records or IPoco because of their mutability, except for the array where it also seems _stupid_ since an empty array is empty
    forever.
The _stupidity_ stands **if** we consider a given final System with a set of concrete, final types. But **if** we consider
those read only definitions from where they are designed, then they appear as "extension points" that may be supported or not.
If we choose this point of view, there is no reason to forbid these properties to exist.

Non writable properties are ignored by serializers/deserializers: they remain purely on the C# side with their default values.

> Abstract Read Only Properties are for the C# side only, they don't appear when Poco are exchanged. [Poco Exchange](PocoExchange/README.md) 
are unaware of these beasts.

