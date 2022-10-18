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

// This is a command part.
public interface ICommandAuthUnsafe : ICommandPart
{
    int ActorId { get; set; }
}
```

### The IClosedPoco

The [`IClosedPoco`](IClosedPoco.cs) interface (that is a `[CKTypeDefiner]` without any property) is a special
marker that expects the final IPoco to be "closed": one of its IPoco definition MUST expose all the properties
of all its interfaces.

A Poco family can have such a "Closure interface" without being a `IClosedPoco`, but if `IClosedPoco` belongs
to the family definition an no closure can be found, it is an error.

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
must be expressed explicitly with messages composed of `Item1`, `Item2`, etc. fields), the lead developer has prohibited the
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

## Poco compliant types

The set of Poco compliant type is precisely defined:

 - Basic types like `int`, `string`, `Guid`, `DateTime`, etc. The definition is [here](../../CK.StObj.Runtime/Poco/PocoSupportResultExtension.cs#L48).
 - Other `IPoco` objects (through any interface or the base `IPoco` interface).
 - Value tuples of compliant Poco types.
 - `List<>`, `HashSet<>`, `Dictionary<,>` and array of Poco compliant type.
 - Formally `object` is allowed provided that at runtime, the instance must be a Poco compliant type.

### The PocoRecord

A PocoRecord is a mutable struct. It aims to capture "micro local types". They are detailed below.

#### ValueTuple

A value tuple is like an anonymous type that locally defines a small structure. The following
pattern is quite common:
```csharp
class AmIWrong
{
    public (int Power, string Name, List<int> Values) Simple { get; set; }
}
```
However, there's something wrong here about nullability. Despites the non nullable string and list, `Name` and `Values` are
null by default and absolutely no warning of any kind will help the developer see this.
Another aspect that can be surprising is that a ValueTuple is a... value type. Its individual fields cannot be set independently:
the whole tuple has to be reassigned to a new one. Care must be taken to copy the "previous" other field values from the original
one.
In a IPoco, value tuples must be `ref` properties:
```csharp
public interface IWithValueTuple : IPoco
{
    ref (int Power, string Name, List<int> Values) Thing { get; }
}
```
Initial values are guaranteed to follow the nullability rules (here Name will the empty string and
initial Values will be a ready-to-use empty list).

> The `ref` enables the individual fields to be set and the tuple then becomes a "local" sub type, an
> "anonymous record". 

To unify the "data shape" (with the `ref`) and preserve the semantics of the "initial value for non nullable fields",
value tuples cannot be nested. The following is an error:
```csharp
public interface INVALID : IPoco
{
    ref (int Power, (string Name, List<int> Values) Rest) Thing { get; }
}
```

For more information on value tuple and more specifically their field names, please read this excellent
analysis: http://mustoverride.com/tuples_names/. The Poco framework handles the field names so that
they can be exploited by de/serializers and importers/exporters if needed.

#### The "record struct"
The `record struct` introduced in C#10 used with the positional parameters syntax is quite isomorph to a
ValueTuple since they are mutable value types with 2 added benefits:
- the default parameter values naturally express the field default value;
- being an explicitly named type it acts as reusable definition of its set of fields.

Record struct have generated value equality and ToString method (but we don't really care here): it is only the
syntactic sugar for the constructor with positional parameters that is of interest here to easily describe the
data structure.

The previous example can easily be rewritten with a reused `record struct` for 2 properties:
```csharp
public interface IWithRecordStruct : IPoco
{
    public record struct ThingDetail( int Power, List<int> Values, string Name = "Albert" );

    ref ThingDetail Thing1 { get; }
    ref ThingDetail Thing2 { get; }
}
```
Note that even if `record struct` and value tuples are equivalent at this level, they are different. There
are no field aliases `Item1`, `Item2`,... available in record struct, only the property names matter (this is
somehow the reverse as how the Value Tuples work).

A word about the `record class`: it is useless for us here since it is a reference type and immutable by default.
To have it mutable, the standard syntax must be used: there is no real difference between defining a `IPoco` and
a `record class` (and also no difference in the final implementation: it's a class with its backing field):
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

#### "record struct" and Value Tuples are "PocoRecord"
Value Tuples and `record struct` are finally the same for the Poco framework: they are value types that
share the same restrictions:
- Must be fully mutable.
- Must be exposed by `ref` properties on `IPoco`.
- Must contain only Poco compliant field types except other Records.

They can appear in collections.

> Composing PocoRecord requires intermediate `IPoco` types.

### The "standard collections
IPoco can expose `T[]`, `List<T>`, `HashSet<T>` and `Dictionary<TKey,TValue>` where `T`, `TKey`
and `TValue` are Poco compliant types.
A `List<(string Name, Dictionary<IPerson,(int[] Distances, IPerson[] Friend)> Mappings)>` is valid.

Note that only these 3 types are supported. Abstractions like `IReadOnlyList<T>` are not allowed to appear in
any type definition.

### IPoco properties

A IPoco property can be writable `{get; set;}` or read only `{get;}` and can be nullable or not.

Whether they are writable or read only, a non nullable property type requires an initial non null value
(otherwise the newly created Poco will not be valid): only some properties can be not nullable.

#### Non nullable properties: the initial value

Nullable properties are the simple to handle: their initial value is always the `default` for
the type and that is null for reference types.

Non nullable properties MUST have an initial value. For value type, this is not an issue since here again
the `default` for the type is the "natural", expected initial value.

When a property type is a **not nullable reference type**, then the constructor MUST
be able to assign a new instance to it, otherwise the newly created Poco will not be valid.

The only true basic type that is a reference type is the `string`, the
[`[DefaultValue]`](https://learn.microsoft.com/en-us/dotnet/api/system.componentmodel.defaultvalueattribute)
attribute must then be used to provide a default value. The empty string being a quite natural "default" for a
non nullable string, if no `[DefaultValue]` exists, the empty string is automatically used.
The other basic type that is a reference type is the `object`: there MUST be a `[DefaultValue]` attribute
defined for all non nullable `object`.

For any other non nullable property types (records, collection, IPoco) the constructor must be
able to synthesize a default instance.
- For `IPoco`, it must be a "concrete" interface, not an "abstract" one (that is a `[CKTypeDefiner]`).
- For collections (`List<>`, `HashSet<>`, `Dictionary<,>` and arrays), the constructor creates an empty instance of 
  the type (for arrays, the `Array.Empty<T>()` singleton is used). 
- For record struct (with positional constructor syntax), the default is the default value parameter or a default
  must be resolved.
- Value tuples are like record struct with no default value: we must be able find an initial value for 
  all non nullable fields of the tuple (and without the help of the `[DefaultValue]` attribute).

#### Writable & Read only properties: the "Abstract Read Only Properties"
A read only property can appear on one or more IPoco interface of a family and also exists
in a writable form on other interfaces. When this happens, the "writable wins": the Poco's
property is actually writable. The read only definitions become "hidden" by the writable ones
ans we call them "Abstract Read Only Properties".

All writable definitions of the same property must be exactly the same: they are strictly type invariants
(including nullability).

A read only property that has a writable definition somewhere in the Poco family becomes "Abstract": its type
doesn't need to be exactly the same as the writable type anymore, it can be any type that is "assignable from",
"compliant with" the writable one.

This concept may seem overkill (and it somehow is) but the goal is to exploit the `IPoco` capacity: a `[CKTypeDefiner]`
attribute on a `IPoco` defines a kind of "abstract" definition. Such definer SHOULD be able to carry "abstract projections"
by exposing read only properties:
  - An `object Identifier { get; }` that can be implemented (at the "concrete" `IPoco` level) by a 
    `int Identifier {get ; set; }` for a family and by `string Identifier { get; set; } for another one
    (this is supported).
  - A `IReadOnlyList<X> Things { get; }` that can be implemented by a `List<Y> Things { get; }` where X 
    is assignable from Y (this is NOT supported).

This is all about covariance of the model (the latter example relies on the `IReadOnlyList<out T>` **out**
specification). Current implementation prohibits this because it is not as easy as it seems to implement (for
instance `IReadOnlyList<object>` on a `List<int>`: value types requires an adapter, a wrapper instance, even
if `typeof(object).IsAssignableFrom( typeof(int) )` is true,
`typeof(IReadOnlyList<object>).IsAssignableFrom( typeof(IReadOnlyList<int>) )` is false).


True "Abstract Read Only Properties" support is not currently implemented. As of today, their type must
be exactly the same as the writable one except:
- that their nullability can differ: a nullable read only is compliant with its not nullable writable (the revert is not true).
- that their type can be object: this is the ultimate variance support and is currently the only supported one.

> Abstract readonly property MAY be supported for records: these properties would have to be regular properties
> (not `ref` properties) and may contain a subset of the writable fields.

When all definitions of a property are read only (no writable appears in the family), this property is either:
  - Nullable... and it keeps its default null value forever. This is _stupid_. 
  - Not nullable... and it keeps its initial value forever. This seems _stupid_ for basic types but it is not for collections,
    records or IPoco because of their mutability, except for the array where it also seems _stupid_ since an empty array is empty
    forever.
The _stupidity_ stands **if** we consider a given final System with a set of concrete, final types. But **if** we consider
those read only definitions from where they are designed, then they appear as "extension points" that may be supported or not.
If we choose this point of view, there is no reason to forbid these properties to exist.

## PocoConverter

> Ideas. To be more precisely defined (monitoring, error management).

```csharp
[IsMultiple]
public interface IPocoConverter : ISingletonAutoService
{
}

public interface IPocoConverter<T> : IPocoConverter
{
  IPoco ToPoco( in T o );
  T FromPoco( IPoco o );
}
```

Such converter can accept more than one `IPoco` type as an input and can create different type of `IPoco` on output.
This makes sense.

The following central service can handle all the conversions:
```csharp
public class PocoConverter : ISingletonAutoService
{
  public PocoConverter( IEnumerable<IPocoConverter> converters );
  public virtual IPoco ToPoco<T>( in T o );
  public virtual object FromPoco( IPoco o );
  public T FromPoco<T>( IPoco o );
}
```

## Current limitations of the abstraction

`[CKTypeDefiner]` attribute on a `IPoco`defines a kind of "abstract" definition.

Unfortunately, the current implementation doesn't exploit this as much as it can (the "abstract" aspect has been
overlooked). A definer SHOULD be able to carry "abstract projections" by exposing read only properties:

  - An `object Identifier { get; }` that can be implemented (at the "concrete" `IPoco` level) by a 
    `int Identifier {get ; set; }` for a family and by `string Identifier { get; set; } for another one.
  - A `IReadOnlyList<X> Things { get; }` that can be implemented by a `List<Y> Things { get; }` where X 
    is assignable from Y.

This is all about covariance of the model (the latter example relies on the `IReadOnlyList<out T>` **out**
specification). Current implementation prohibits this because it is not as easy as it seems to implement (for
instance `IReadOnlyList<object>` on a `List<int>`: value types requires an adapter, a wrapper instance, even
if `typeof(object).IsAssignableFrom( typeof(int) )` is true,
`typeof(IReadOnlyList<object>).IsAssignableFrom( typeof(IReadOnlyList<int>) )` is false).

