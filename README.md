# CK-StObj #

This repository contains the basics of the Real Objects, Poco and Auto Services features of the "CK-Database" framework.
Take a deep breadth, there's a lot to process here.

## Real Object
Firt come the Real Objects. Their goal is to represent actual object or concept of the real, external, world: a database,
a cloud subscription, a table in a database, a folder, a git repository: all these beasts exist; they are "Real Object":
 - They are true singletons.
 - They are often immutable and expose pure functions (mutability and side-effect must be handled with care because of concurrency).
 - A Real Object can depend from any number of other Real Objects without cycles: this is a classical [DAG](https://en.wikipedia.org/wiki/Directed_acyclic_graph), but ONLY other Real Objects appear in this graph.
 - A Real Object can "refine"/"extend" a base one (simple inheritance). 
 
 So far so good. But doesn't this look like basic object programming?  

 However, as soon as a specialization exists in a code base, there's one and only one instance of the specialization

## Poco
Then come the Poco (Plain Old C# Objects) that are simple objects. Poco are typically used as [Data Transfer Object](https://en.wikipedia.org/wiki/Data_transfer_object),
they carry data and have no or very few associated behaviors.


