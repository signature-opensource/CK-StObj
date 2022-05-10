# CK-StObj

This repository contains the basics of the Real Objects, Poco and Auto Services features of the "CK-Database" framework.
This is a very high-level overview. To grasp the whole picture, take a deep breadth: there's a lot to process under the
tip of the iceberg.

## Real Object
First come the Real Objects. Their goal is to represent actual object or concept of the real, external, world: a database,
a cloud subscription, a table in a database, a folder, a git repository: all these beasts exist; they are "Real Object":
 - They are true singletons.
 - They are often immutable and expose pure functions (mutability and side-effect must be handled with care because of concurrency).
 - A Real Object can depend from any number of other Real Objects without cycles: this is a classical [DAG](https://en.wikipedia.org/wiki/Directed_acyclic_graph), but ONLY other Real Objects appear in this graph.
 - A Real Object can "refine"/"extend" a base one (simple inheritance). 
 
So far so good. But doesn't this look like basic object programming?  
Yes... with one major difference: these types are analyzed by the StObjEngine and one and only one most specialized type must exist for any
object hierarchy. This applies to interfaces as well as classes: a `public interface IUserTable : IRealObject` is necessarily, eventually, a singleton.
Any ambiguous implementation triggers an error.

RealObjects implementations are often _abstract class_ with _abstract_ methods: their final implementations are generated by "Aspects" thar are StObjEngine's
plugins.

RealObjects are _de facto_ singleton services (and are registered as such in the DI container) but they can only depend on
other RealObjects. They are used as backbones of the system: their dependency graph is used to structure and organize features
like code generation, database setup and migration, external scripts execution, etc.
 
Please see [here](https://github.com/Invenietis/CK-Core/tree/develop/CK.Core/AutomaticDI#irealobject) for more details.

## Poco
Then come the Poco (Plain Old C# Objects) that are simple objects. Poco are typically used as [Data Transfer Object](https://en.wikipedia.org/wiki/Data_transfer_object),
they carry data and have no or very few associated behaviors.

Poco are basic interfaces that extend the `IPoco` interface marker. They exposes properties that must be of "Poco compliant" types.
Poco compliant types are:
 - Basic types like `int`, `string`, `Guid`, etc.
 - Other `IPoco` objects (through their interface).
 - Value tuples of compliant types.
 - A class decorated with the `[PocoLike]` attribute.
 - `List<>`, `HashSet<>`, `Dictionary<,>` or array of Poco compliant objects.
 - Formally `object` is allowed: at runtime, the instance must be a Poco compliant type.

This `IPoco` concept fulfills 2 goals:
- Supporting true modularity: any modules/packages can enrich any `IPoco` interface independently of others.
- Defining a "closed world" of DTO that are the only ones that should be authorized to be exchanged with external parties.

By allowing independent packages (packages that don't know each other) to simultaneously extend the same _eventual_ type,
the `IPoco` interface is one of the key of the *Package First* approach.

## Automatic DI

Last but not least, the Automatic Dependency Injection aims to relieve the developer of the burden of DI configuration.
Basically, by using the [IAutoService](https://github.com/Invenietis/CK-Core/blob/develop/CK.Core/AutomaticDI/IAutoService.cs)
marker interface (along with `ISingletonAutoService` and `IScopedAutoService`), and the [IsMultiple](https://github.com/Invenietis/CK-Core/blob/develop/CK.Core/AutomaticDI/IsMultipleAttribute.cs)
attribute, the DI container can be automatically optimally configured (every service that CAN be singleton will be registered as singleton). 





