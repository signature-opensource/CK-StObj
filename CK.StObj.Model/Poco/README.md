# Poco

## Poco compliant types

 - Basic types like `int`, `string`, `Guid`, `DateTime`, etc. The definition is [here](https://github.com/signature-opensource/CK-StObj/blob/master/CK.StObj.Runtime/Poco/PocoSupportResultExtension.cs#L48).
 - Other `IPoco` objects (through any interface or the base `IPoco` interface).
 - Value tuples of compliant Poco types.
 - A class decorated with the `[PocoClass]` attribute.
 - `List<>`, `HashSet<>`, `Dictionary<,>` or array of Poco compliant objects.
 - Formally `object` is allowed provided that at runtime, the instance must be a Poco compliant type.

## The IPoco families
Any interface that directly extends [`IPoco`](IPoco.cs) defines a "family" of types that will eventually be implemented
by an automatically generated concrete class that will support all the interfaces of the family.

A property with the same name can be defined by more than one interface in a family: in such case, its type and read/write access
must be the same. If a [DefaultValue(...)] attribute is specified on one of the property, all other defined default values must
be the same.

### Generic IPoco

A IPoco family cannot be defined by a generic interface. If this was possible different extensions could use
different types for the same type parameter.

This is forbidden:

```c#
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

Using the `[CKTypeDefiner]` attribute enables a generic definition of a "family of family". This is how 
Commands and their results are modeled by CRIS:

```c#
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

## [PocoClass] classes

A class with a `[PocoClass]` attribute SHOULD behave just like a IPoco instance.
Restrictions apply to a PocoClass class so it "looks like" IPoco and can be handled the same way:

- It can be abstract: this base type will be Poco compliant (but cannot obviously be used as a read only property).
- Concrete classes MUST have a public default constructor since:
  - having to deal with constructor parameters while restoring an object graph is rather complex;
  - this enables a PocoClass object to appear as a read only property (the constructor of the property owner can instantiate
    the property instance).
- Its public properties MUST be of Poco compliant types.

Each class of a hierarchy MUST declare the attribute (the attribute doesn't inherit). Specialized classes that haven't the attribute
are not Poco compliant.

> This opt-in approach has been chosen to avoid leaks or security holes. The ultimate set of Poco types is a strictly defined
> closed world.


## Future...
Currently, only IPoco and [PocoClass] are handled. One may introduce a [PocoLikeSupport] or [PocoLikeImplementation] or other
mechanisms to handle types that cannot support these restrictions that may use external services to support the required features.

One funny way to extend the support would be to introduce a `[PocoProjection(typeof(T))]` (may be with a `where T : IPoco` constraint).
The developer will only need to describe a IPoco with a subset of the object properties and implement a constructor that accepts
this IPoco as a parameter.
The export function can be automatically generated and the constructor enables read only type support.

Another approach: a method `object ToPocoType()` is defined that must return a Poco compliant type. For simple type this
can simply return a value tuple with Poco compliant fields... But the constructor will have to accept an object and this is
far from ideal.
Actually, the returned type can formally be any Poco compliant type since these will be called by auto generated code.

> This approach seems the most interesting one because it can support a complete externalization of the code.

A type `TPocoLike` is either:
1.  a type that supports the [PocoLikeSupport] attribute and has `T ToPocoJon()` and `constructor( T )` 
  members (where T is a Poco compliant type);
2. or a `IPocoConverter<TPocoLike> : ISingletonAutoService` exists that implements the conversion methods, ideally 
   without explicit type constraint (like above).

The n°1 can be implemented more easily than n°2. The latter would require to:
- Wait for the AutoService resolution before being able to compute the transitive closure of the Poco compliant types. This
  will be very complicated (the PocoSupportResult is built early in the process). To workaround this, there's 3 options:
  - Reject: We forbid any new Poco compliant type to appear in the converted type: all types that appear in a converted type must 
    already been registered as Poco compliant ones. This limitation may be acceptable.
  - Check: We can consider that any type that appear in IPoco closure MUST eventually have a converter and check 
    the converters existence after the AutoService resolution.
  - Discover: the `IPocoConverter<TPocoLike>` converters are discovered and analyzed early by the KindDetector and their 
    conversion target is registered as being "PocoLike" or "PocoConvertible".

- Give the PocoDirectory (that is a IRealObject) the IServiceProvider to resolve the converters.

This has to be investigated.
