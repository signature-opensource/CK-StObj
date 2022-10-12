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
- A "Deviant" protocol (for a partner system that requires json object's property to be lexicographically sorted - yes that exists...) 
should be as easy as possible to implement and use.

On the data side, we must be confident that the Poco model IS a superset of what should ever be needed (or be confident that if a
type or a construct is missing, it could be easily added).

- We work in C#: we know our language capabilities, the basic types we need. We can support them once for all.
- We believe that the "Poco compliant types" we support are enough to model any "exchange data" notably with the
support of the support of a union type (the `oneof`) for IPoco property.



## Poco compliant types

 - Basic types like `int`, `string`, `Guid`, `DateTime`, etc. The definition is [here](https://github.com/signature-opensource/CK-StObj/blob/master/CK.StObj.Runtime/Poco/PocoSupportResultExtension.cs#L48).
 - Other `IPoco` objects (through any interface or the base `IPoco` interface).
 - Value tuples of compliant Poco types.
 - `List<>`, `HashSet<>`, `Dictionary<,>` of Poco compliant objects.
 - Formally `object` is allowed provided that at runtime, the instance must be a Poco compliant type.

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

Such converter can accept more than one IPoco type as an input and can create different type of IPoco on output. This makes sense.

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
if `typeof(object).IsAssignableFrom( typeof(int) )`).

