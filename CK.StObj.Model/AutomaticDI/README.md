# Automatic DI

This folder contains types that will eventually be defined in CK.Core.
"Front/Process context" for services and marshalling is not finalized yet: we should be able to model
"endpoint adherence" of front services (there's more than one endpoint) and marshaller may not be able to work across
all contexts.

CK.Core defines: IRealObject, IAutoService, IScopedAutoService, ISingletonAutoService, IsMultipleAttribute, ReplaceAutoServiceAttribute,
CKTypeDefinerAttribute and CKTypeSuperDefinerAttribute.

See https://github.com/Invenietis/CK-Core/tree/develop/CK.Core/AutomaticDI

## The "Endpoint" DI roots

### Step 0 - Some background
The notion of "IEndpointService singleton" doesn't currently exist (may 2323)... I've always considered
"Endpoint services" (formerly called "Front services") to necessarily be scoped dependencies.
That was a mistake (an overlook).
One of the first idea was to hook the service resolution with a configurable scoped service:

```csharp
[IsMultiple]
public interface IEndpointServiceResolver : ISingletonAutoService
{
    object? GetService( IServiceProvider scope, Type serviceType );
}

public sealed class ServiceHook : IScopedAutoService
{
    readonly IServiceProvider _scoped;
    IEndpointServiceResolver? _resolver;
    readonly Dictionary<Type, object?> _resolved;

    public ServiceHook( IServiceProvider scoped )
    {
        _resolved = new Dictionary<Type, object?>();
        _scoped = scoped;
    }

    public void SetResolver( IEndpointServiceResolver resolver )
    {
        Throw.CheckState( _resolver == null );
        _resolver = resolver;
    }

    public object? GetService( Type t )
    {
        Throw.CheckState( _resolver != null );
        if( !_resolved.TryGetValue( t, out object? o ) )
        {
           o = _resolver.GetService( _scoped, t );
           _resolved.Add( t, o );
           return o;
        }
    }
}
```
We cannot register a "IEndpointService singleton" as a singleton and resolves it through from a scoped hook
because of an optimization in the .Net Core DI where registered singleton are directly resolved to the root
(resolving from the requesting scope is short-circuited). To be able to hook the resolution we must register them as
Scoped. But by doing this, they are no more singletons. Anyway, this simply cannot work at all because of the inherent
recursion on the scoped container and hook.

It seems that we need either:
- A more powerful DI engine (Autofac) and use its capabilities but it comes with a fair amount of complexities
  (which is perfectly normal) and requires hooks to be setup to be integrated with the "Conformant DI".
- Continue to use the Conformant DI but take more control on it.

### Step 1 - The Conformant DI choice 

Our need is NOT a multi-tenant management (like Orchard does). Our application is ONE application, an homogeneous
Party that interacts with other parties. The DI we need is about Endpoints that connect the Application to the external
world. We also want "Dynamic Endpoints" (endpoints can appear and disappear dynamically).
What we have so far:
- A Endpoint always creates a Scope to do its job (this is what does ASPNet for each HTTP request).
- There is no issue about regular scoped services and regular singletons (true singletons) but we would like
  to hide some services for some endpoints: not all the services must (and even can) be available from all the endpoints
  (think to the awful AsyncLocal-is-evil `IHttpContextAccessor`). This "hiding" is not per endpoint instance but per endpoint type:
     - A MQTT endpoint on port 51634 must provide the same "MQTTAccessChecker" or "MQTTMessageStore" singletons as the
       one on the port 6871.
     - All MQTT endpoint must see the same singletons instances and have access to the same set of service types, including the
       scoped ones. For the scoped services, it is the responsibility of the endpoint to provide potentially configured
       service instances based on its configuration and/or the external world it is dealing with (a `IMQTTCallerInfo` scoped
       service can expose a RemoteAddress property for instance).
- The "EndpointDefinition" is a perfect candidate to be a IRealObject that can give access to the IServiceProvider (or a factory of)
  that must be used by any of its endpoint instance (so that a Scope can be created from it).

### Step 2 - Conformant DI and endpoint services

We decided to reuse the Microsoft DI container (and its philosophy). The issue now is to
handle the configuration of these DI container and/vs. the "global/primary DI" that .Net Core hosts uses.

Currently, any IRealObject can have a 'void ConfigureServices( StObjContextRoot.ServiceRegister, ... )' method that
enables real objects to configure the DI (registering new services, configuring things, etc.) based on any number
of parameters that can be any other real objects and/or startup services previously registered.

This is all about configuring the "global/primary DI" to allow standard .Net applications to work seamlessly with
the Automatic DI. Here, we need to:
 - Preserve this as much as possible, except that EndpointDefinition specific services should not be registered in it.
 - Setup EndpointDefinition DI containers with their specific types and all the common ones.

The "Global DI" build is currently out of our control (to avoid the required use of IServiceProviderFactory) and
this is a good thing. If we want to minimize the changes here, we must at least find a way to "tag" the services
that are bound to the regular .Net Core endpoint (often the "WebEndpoint") to not register them in the other endpoint's
containers. For the "Web", these are the services that ultimately depend on the HTTP layer and not too many are like this.

Currently (may 2023), IsEndpointService can be set externally (SetAutoServiceKind and by configuration). The
IsEndpointService semantics must be changed a little bit: it doesn't imply a Scope lifetime anymore (but still
implies the IsProcess service for the future "marshalling" capability).

When a service is marked as a IsEndpointService, it means that we know what it is, we know that it has an
adherence to some specific aspects of the system... Is it enough to orchestrate the whole thing?

- If it is a scoped service, it is up to a EndpointDefintition to claim that it handles it: this is
  an opt-in, explicit, statement (by attribute for instance) since it will have to register a way to
  instantiate the scoped service from its own container.
- If it is a singleton, it must be the same instance if other endpoints also want/need to support it. This can
  be the one of the Global DI or must be shared between the EndpointDefinitions that want it and not appear in the
  Global DI.

The idea of the process should look like:
- During the static type analysis, we collect all the existing EndpointDefinition.
- A EndpointDefinition must claim any "specific" endpoint service it handles. Let's do this with a (AllowMultiple) attribute:
    [SpecificEndpointService( Type t )]
    This states that endpoint instances will be able to provide the `t` service AND that this type is specific
    (but not exclusive) to the EndpointDefinition.
    Two different endpoint can claim to support the same "specific" endpoint service type (if exclusivity must be
    supported, it will be rather easy but we don't need it here).
- We use this opt-in claim to flag all the services with at least one such "Specific" declaration: these services
  must disappear from the "Global DI".
- When we AddStObjMap/AddCKDatabase, we remove any ServiceDescriptor with a "specific type" from the ServiceCollection:
  the Global DI won't be able to resolve these types.

### Step 3 - Global DI is the "DefaultEndpointDefintion"

We must now consider the EndpointDefinition container initialization. To rely/reuse the Conformant DI infrastructure, we must
reason only in terms of ServiceCollection configuration. Each container will be obtained through the `IServiceCollection.BuildServiceProvider()`
extension method: once built, the container is sealed, it cannot be altered.

A EndpointDefinition container must be able to return singletons:
 - from the global DI if the type is not a IsEndPointService or is a IsEndPointService that is not "specific".
 - from a shared container for IsEndPointService that is not exclusive to the endpoint.
 - from its own container otherwise.

And scoped services:
 - These are "parametrized services". Their parametrization can come from the endpoint instance (a "EndpointAddress" property)
   or from a more dynamic context (a "CallerAddress" from one of the many connections that the endpoint is managing).

Actually, singletons are also "parametrized" by the EndpointDefinition itself and this raises a question about a singleton shared 
by 2 or more endpoints: somehow, the one who's in charge of its initialization "wins", the other ones have nothing to say about
the properties/configuration of the service (necessarily stable because it's a singleton). This is problematic: we cannot
trust the developer to ensure the coherency of different singleton initialization, it should be the same "by design", initialized
by one and only one EndpointDefinition. Following this path leads to a simple conclusion: the "Global DI" is a container like the
others, nothing makes it "special" or more "global" than any EndpointDefinition's container. However, if the "WebEndpointDefinition" exists,
it is unfortunately not exactly the same as the other ones because its container is managed "above", by the application host
(should it be named "HostEndpointDefinition"?) and it's configuration (the ServiceCollection that has been configured by Startup
methods) is the basis of the other ones.

Should we model this "HostEndpointDefinition" ("DefaultEndpointDefinition" may be a better name)? Or should we keep the idea described above
that considers the DefaultEndpointDefinition's "specific" services to be by default the services that are not marked with the
[SpecificEndpointService( Type t )]` attribute on any other EndpointDefinition?
If we model it, it must expose its final ServiceProvider like the others. It means that there must be some code somewhere
that sets the host's final service provider on it. Unfortunately, there's no "OnContainerBuilt" event or hook exposed by
.Net Core DI and using a IHostedService is not a perfect option since even if StartAsync is called right after the container
initialization there is no ordering constraints. It seems that we cannot model it, its handling must be specific.

### Step 4 - We need IHostedService

BUT! A EndpointDefinition must be able to configure its container to resolve a singleton from the global DI container
with something like that:
```csharp
myContainerBuilder.AddSingleton( typeof( ICommonSingleton ), _ => _theGlobalDI.GetService( typeof( ICommonSingleton ) ) );
```
This means that EndpointDefinition must have access to the global DI before being able to resolve even a common singleton. EndpointDefinitions
are IRealObject, their constructor have no parameters by design. We need a singleton service to capture the global DI container (in its constructor)
and the instantiation of this service must be triggered by someone: we are stuck with the `IHostedService` execution to initialize the
EndpointDefinitions, we have no choice... So, we *can* model the "DefaultEndpointDefinition". If we do model it, it can describe its own specific
endpoint services (like `IAuthenticationService` that requires a `HttpContext` to do its job), de facto removing it from the set of
services of any other EndpointDefinition.

### Step 5 - Reverting the definition with attributes on the services.

BUT (again)! This `IAuthenticationService` must be preempted by the DefaultEndpointDefinition only if it exists, that is if the host is
a web application. This would require a "late binding" approach of these services (based on the Assembly Qualified Name of the type,
similar to the configuration of AutoServiceKind for well known external services) that must be as exhaustive as possible.
This doesn't seem sustainable. We can change how a "specific endpoint service" is tied to its EndpointDefinition and be more powerful
(and may be more explicit): with one attribute on the service type itself `[EndpointService( Type endpointDefinition )]` and one assembly
attribute `[assembly:EndpointServiceType( Type serviceType, Type endpointDefinition )]` we reverse the declaration and use strong type
to declare the association.

One can take this opportunity to add a `bool exclusiveEndpoint` to the attribute parameters to prevent any service sharing
when it doesn't make sense for the service to be available in any other EndpointDefinition. When `exclusiveEndpoint` is false,
we are left with an important issue: a "shared singleton" must be initialized by one and only one EndpointService, the others
may reuse/expose it but it should come from a single *Owner* container. We can express this by refining the attributes:
- `[EndpointServiceImplementation( Type endpointDefinition, bool exclusiveEndpoint )]`
   and `[assembly:EndpointServiceTypeImplementation( Type serviceType, Type endpointDefinition, bool exclusiveEndpoint )]`.
   These attributes specify the single owner/creator of the service.

But the notion of ownership (and the `exclusiveEndpoint` for sharing or not) only applies to singletons. They'd better be named:
- `[EndpointSingletonServiceOwner( Type endpointDefinition, bool exclusiveEndpoint )]`
   and `[assembly:EndpointSingletonServiceTypeOwner( Type serviceType, Type endpointDefinition, bool exclusiveEndpoint )]`.

The assembly scoped attribute may seem useless however it is required to define the `IAuthenticationService` to `DefaultEndpointDefinition`
association. In practice, this assembly attribute will be used for the DefaultEndpointDefinition, but it doesn't cost much to keep it as-is.
Thanks to this, any assembly that is tied to a "host capability" can participate in these associations. The CK.AspNet.Auth
assembly for instance can declare the `IAuthenticationService` to be a endpoint specific (and exclusive!) service of the
DefaultEndpointDefinition.

Scoped services have no "shareability" issue, so why do we care about tying a service to one (or more) EndpointDefinition?
Because we want to be able to reason about the services for 2 different reasons:
- We want to "frame the developer's work", to detect inconsistencies and errors as early and as automatically as possible.
- If we precisely know the set of services that is supported by a endpoint, then we can check the availability of a command
  handler or any other methods and/or service for the endpoint at setup time. For Cris, this enables us to compute the exact
  set of commands that a endpoint supports. (We can even imagine automatically selecting a constructor or a method based on
  its signature but this may be weird).

To reason about services, we do need `[EndpointAvailableService( Type endpointDefinition )]` and
`[assembly:EndpointAvailableServiceType( Type serviceType, Type endpointDefinition )`. The good news is that they apply
to singletons and scoped services: we don't need more. It is important to understand at this point that any "regular"
service (that is not tied to an EndpointDefinition) be it singleton or scoped MUST be available from all the endpoints.
If a service (like the `IAutehticatonService` for instance) cannot do its work from all endpoints, then it is a "endpoint service"
and this requires a fix:
- Declaring it to be available in the DefaultEndpointDefinition (this tags the service to be IsEndpointService).
- Declaring it to be available in any EndpointDefinition that can expose it (and do the job of actually supporting it in
  every possible EndpointDefinition).

### Step 6 - Endpoint services are NOT automatic services.
A singleton marked with one or more `EndpointAvailableService` and no `EndpointSingletonServiceOwner`
is an error (we have no owner for it). This first limitation breaks the `IAutoService` magic that automatically
computes the lifetime based on the dependencies but this limitation concerns only singleton endpoint services.
Endpoint is an "advanced" concept: one won't implement an EndPoint every day and defining a service for an endpoint
is a special task. We then decide the following:
- ISingletonAutoService and IScopedAutoService interface markers can still be used: they settle the lifetime and any incoherency
  will be detected.
- IAutoService alone has no real effect: either there is a `EndpointSingletonServiceOwner` attribute somewhere that sets it as
  a singleton, or there are only one or more `EndpointAvailableService` declarations that set it as a scoped service.

We have skipped an important point: the "Endpoint services and specialization" issue. This is not trivial... at all:
- Can a specialized endpoint service type extends its availability to more EndpointDefinition than its base type?
- IAutoService implies a final unique (non ambiguous) chain of inheritance. Fully supporting IAutoService (even for scoped service)
 would require to scope the graph resolution to each EndpointDefinition context.

Our current intuition is that trying to fully support the IAutoService capabilities for endpoint services will ultimately lead to
multiple independent DI containers, a situation where the "singleton" semantics will be lost or "diluted" in too much complexity.

BUT! We must allow a some general capability expressed as a mere (non endpoint) interface to be implemented by different concrete
classes in different EndpointDefinition. 

Do we need to totally forget the "singleton ownership" and follow the "scope the graph resolution to each EndpointDefinition context"
way?
This is tempting:
- We would have "contextual endpoint singletons" that live their life in each context.
- True singletons still exist: they come from the "Global DI".
- EndpointDefinition would simply rely on a `EndpointAvailableService` that states that the type is a endpoint service (regardless of
  its lifetime) in a given EndpointDefinition. And this reintroduces one of the previous idea: this endpoint service must now be explicitly
  allowed in any other EndpointDefinition to be available.

This deeply change the current implementation but in such a "global" way that it may paradoxically be doable. However after
(too) many hours of investigation, it appears that the current implementation is not ready to support this easily.
The "AutoService" step occurs too deep, returning a AutorServiceCollectorResult that has already resolved the most specialized
class. Contextualizing the whole service graph first requires a big refactoring to lift this step up in the process.

We then keep the proposed approach based on the 4 attributes and act that endpoint services are not automatic services at all
(this is a stronger assertion than previously stated):
- Either there is a `EndpointSingletonServiceOwner` attribute somewhere that sets it as a singleton, or there are
  only one or more `EndpointAvailableService` declarations that set it as a scoped service.
- ISingletonAutoService and IScopedAutoService interface markers can still be used: they settle the lifetime and any incoherency
  will be detected.
- When a Type is declared as an endpoint service, IAutoService marker is erased and this also applies to its specializations
  that becomes de facto endpoint services.
- A type that is endpoint service because at least one its ancestors is and have no `EndpointSingletonServiceOwner` nor `EndpointAvailableService`
  is problematic: it should not exist since it shouldn't be available from anywhere. We currently track these "orphan endpoint services" and
  remove them from the global container (that is aggressive, this could be changed if this happens to not be a good idea).

> Endpoint services are not IAutoService. Endpoints live in the "standard DI world" where, for instance, one must register
"by hand" the concrete type as well as all its generalizations that must also be registered.

This "by hand" aspect is rather surprising when you are used to the Automatic DI. The only available rules are that once a type is a
endpoint service:
  - Its lifetime is settled.
  - Its specializations are also endpoint services with the same lifetime.

And that's all. For instance, this is legit:
```csharp
[EndpointSingletonServiceOwner( typeof(DefaultEndpointDefinition), exclusive: false )]
public interface ISomeService { ... } 

[EndpointSingletonServiceOwner( typeof(AnotherEndpointDefinition), exclusive: true )]
public interface ISomeRefinedService : ISomeService { ... } 

[assembly:EndpointAvailableServiceType( typeof(ISomeService), typeof(AnotherEndpointDefinition) )]
```

The `ISomeService` is a singleton (at least this cannot be changed by `ISomeRefinedService`), but `ISomeRefinedService` is
a different singleton (a different instance) in AnotherEndpointDefinition that also expose the `ISomeService` **from** Default.
The `ISomeRefinedService` is split in two: its base part implementation is provided by the singleton in Default and its
specialized part is provided by the instance in Another. This is "normal" in the standard DI world. Some developers/architects
may call this a feature, for us it is a recipe for disaster.

### Step 7 - Attributes settle Lifetime and removing "exclusivity".

This example raises a question about the name and semantics of the `EndpointAvailableService` attribute: it doesn't express
the lifetime and, after some tests, this definitely introduces an ambiguity. Also, a serious issue appears in the previous example:
we have here 2 instances of the `ISomeService` (one implementation in Default and one in Another) and the current set of attributes
cannot state that YetAnotherEndpointDefinition wants to expose the `ISomeService` of `Default` (rather than the one in Another). 

It is better to rethink the attribute a little bit now that we have accepted "how much" endpoint services are not auto services
(but at least settle the service's lifetime). Attributes should clearly express the lifetime and singleton attributes should
express the "owns vs. reuses (as a kind of fallback) from another endpoint" aspect.

This is the opportunity to question the "exclusive" aspect. Since we are "into the wild", this declarative aspect is a bit of an
alien in the new landscape. Moreover, this simple boolean breaks (or at least imply questionable choices) a feature that is now
possible for endpoint configuration: convergence.

This part of the (not so) Automatic DI configuration can be totally independent of the registration order. Convergence (that can
be seen as a kind of "Super Idempotence") is always a good thing, it annihilates any risk of misbehavior following a change in
the architecture. We only have to decide how multiple configurations coalesce into the final one:
- When multiple owners exist for a service type in one endpoint definition:
  - If a null is in the candidates, it wins: the fact that a endpoint owns a service is stronger than reusing/exposing the service
    managed by another endpoint.
  - If there's no null in the candidates, this is an error.

And that's it. This simple combination rule is enough. Supporting the "exclusive" here can be done but this flag will lose nearly
all of its interest since a "true" can be replaced by a "false" (and vice-versa) depending on how you consider the "coalescing" of
the configuration.

Let's remove it. This lead us to the 2 following attributes:
- `[EndpointScopedService( Type endpointDefinition )]` is enough for scoped services.
- `[EndpointSingletonService( Type ownerEndpointDefinition, Type? ownerEndpointDefinition = null )]`
  - By default, when `ownerEndpointDefinition` is null, this defines a "regular" implementation by a owning endpoint.
  - When the owner is specified, this defines a "reuse", a binding, to the singleton owned by another endpoint.

And their `[assembly:EndpointScopedServiceType( Type serviceType, Type endpointDefinition )]` and 
`[assembly:EndpointSingletonService( Type serviceType, Type ownerEndpointDefinition, Type? ownerEndpointDefinition = null )]`
for assembly-level declaration.

This is clearly not for mundane developers. But the funny thing is that this exactly what can be done with the basic DI...
The [tests](../../Tests/CK.StObj.Engine.Tests/DI/ViolatingTrueSingletonRuleTests.cs) here shows how a service instance can
be split in 2 parts.

### Step 8 - Endpoint definition vs. instance: removing ownership.

While implementing the current system as described above, it appeared that the ownership of singletons is not that
interesting but most importantly, is not really bearable. The issue is between the "endpoint definition" and
the "endpoint instance" notion. The definition captures the required work at the `IServiceCollection` level:
what are the registered services and how they should be instantiated. This definition is, and must be, independent
of the endpoint instance that will use this configuration to setup is DI container.

We need endpoint instances that are NOT singletons (nor scope), their lifetime is totally independent: instances should be able to be created
at any time with any specific state/configuration. One instance of the (non implemented) `AlternateHttpEndpointDefinition`
may exist on port 4567 and another one may temporarily exist on a random port to support a temporary web
application for instance. 

With this mind, it seems obvious that the singleton ownership is not even possible: from which endpoint instance
should the bound service be retrieved? The service that wants to expose the service must be able to select the
right container, it must know "something" about the endpoint instance that owns the service to get it...
It is not the "ownership/binding" idea that is false, it is its expression based on definition that is totally
useless!

If "singletons service binding" across container must be done, it is up to the definition to do it the way they
want. The only thing we have to provide to a definition is a way to obtain any other endpoint definition they may
need (this is easy because definitions are IRealObject) but also endpoint instances that are currently "alive"...
This means that "alive endpoint instances" are referenced (globally in the `EndpointTypeManager` or by each
`EndpointDefintion`). Doing this introduces an obvious point of failure: how the lifetime of the endpoint
instances is managed? If a endpoint instance `A` exposes a service owned by another `B` instance, `B` must be
alive until `A` dies, or (and this may often be the case), this service is stateless (or its state is somehow
unrelated to the endpoint instance specific data). But how can we know? We cannot. Unless the service
implementation (in a endpoint, not the service interface or abstract class) tells us its "lifetime regarding
its endpoint" status that can be for instance:
- NotShareable
- EndpointIndependent
- EndpointDependent

Whatever this declarative aspect is, it is the endpoint that expose the service that ultimately decides how
the service is resolved and whether its lifetime is correctly managed. The only basic mechanism that can be
implemented is way to "lock" and "unlock" another endpoint instance lifetime (typically using a reference
counting). All of this can be implemented later: for the moment we simply remove the "owner" aspect from
the singleton declaration.

### Step 9 - The EndpointType and EndpointServiceProvider.




We cannot use the standard `IServiceScopeFactory` because it doesn't support scoped data injection.

Ubiquitous endpoint services are currently not managed. `IActivityMonitor` is one of them and it is up
to each endpoint definition to handle it.








It is the service that now claims to be tied to a EndpointDefinition. When it is the DefaultEndpointDefinition we cannot do much, but
for the other ones, we may now check that they really support it: ensuring at setup time that the System is coherent is
an important aspect of the Automatic DI. To do this, the fact that a EndpointDefinition handles a service type must be discoverable
by reflection. A `[EndpointDefinition( params Type[] types )]` on the specialized EndpointDefinition should be enough.

It is now time to better describe how a EndpointDefinition does its magic. Because of the required `IHostedService.StartAsync`
used as the "OnGlobalContainerBuild" hook, everything starts (at runtime) with the singleton Auto service `EndpointTypeManager`
that captures the global DI container and the DefaultEndpointDefinition real object instance through its constructor.

This `EndpointTypeManager` is a `IHostedService` with a no-op StartSync method: the constructor is enough (we don't need
an async context here), the hosted service is only here to trigger the type resolution from the global DI container as early as possible.

The `EndpointTypeManager` constructor calls an internal `SetGlobalContainer( IServiceProvider )` method on the DefaultEndpointDefinition
that memorizes the global container as its own one and relays the call to all the other existing EndpointDefinition that can now use the
global one to do their job... and we are done.

What?!  
-
Of course, this is the only the final runtime part. The real (and hard) work has been done before.
Before that, our "BeforeContainerBuild" hook on the IStObjMap has been called by the AddStObjMap
extension method and has provided the global `IServiceCollection` to all the EndpointDefinition instances.
The EndpointDefinition instances have used it to configure their own `IServiceCollection` and build from it
their own container. The global `IServiceCollection` is then cleaned up of all the specific endpoint services
and returns to the host that will build the final global container... and we are done.

Yes... except that the "build their own `IServiceCollection`" step is not trivial. It's code has
been generated by the setup based on the static type analysis as well as the code of the "cleaned up of
all the specific endpoint services" for the global service collection.

__Remarks__:
- Before Endpoint support, CK.StObj.Model had no dependency on `Microsoft.Extensions.Hosting.Abstractions`:
the OnHostStart/Stop[Async] support on IRealObject is an optional feature that kicks in only if at least one
`IRealObject.OnHostStart/Stop` is used. Because of the `EndpointTypeManager`, CK.StObj.Model now requires this dependency.
- We lied about the fact that the `EndpointTypeManager` was a `IHostedService`: actually there is only a
`EndpointTypeManager` (abstract class). The actual hosted service is the code generated `HostedServiceLifetimeTrigger`
that has been extended to handle the EndpointTypeManager that is also fully code generated. But this is
an implementation detail (to have the cleanest possible CK.StObj.Model API) and doesn't change the principle.






