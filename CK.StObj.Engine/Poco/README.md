# Poco Type System

## Oblivious Types
Oblivious types is a subset of all the IPocoType. Their goal is to expose a smaller set
than all the types to ease code generation. A code generator can always work with 
all the types but there are less Oblivious types and they can be enough for some processes.
Oblivious types can be seen as Canonical types.

An Oblivious type is necessarily not nullable. Handling a nullable type once the non
nullable is handled is often easy (for instance when deserializing, handling a nullable
is a "shell" that reads the "null" marker and returns null instead of throwing).

The Oblivious type of an anonymous record 

An Oblivious type is only composed of Oblivious types.

