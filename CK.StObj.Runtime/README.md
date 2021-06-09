## The "Setup dependency" separation

From "Model" assemblies, everything starts with an attribute: the [ContextBoundDelegationAttribute](../CK.StObj.Model/Support/ContextBoundDelegationAttribute.cs),
a lightweight attribute that simply contains the Assembly Qualified Name (a string) of another Type that must "replace" the attribute.

This enables "Model" assemblies to also be as lightweight as possible: Model objects have absolutely NO dependencies on code generators or
any setup mechanism, as "Models" they solely focus on:
 - Describing reality, things that must exist in the System: "Hey! I'm an Azure table (in a Storage Account) that hold users data!".
 - Exposing functionalities, capabilities of those things: "You can call this method to add or updates a User data.".

The *implementations* types that the attributes target reside in Engine (or Runtime) assemblies that are dynamically loaded during the Setup phase:
they exist only during Setup to build the reality (creating Sql or powershell install or upgrade scripts for instance) and/or to generate code that
will automatically implement functionalities (based on the "Model", typically by providing the implementation of an abstract method: the Model is
the method signature).

Actually, the fact that the *implementations* are in external assemblies is not required (this is to obtain the leanest and meanest possible *runtime*
System). For tests, we can define the *implementation* next to the *ContextBoundDelegationAttribute*.

