# Automatic DI

This folder contains types that are not defined in CK.Core.

CK.Core defines: `IRealObject`, `IAutoService`, `IScopedAutoService`, `ISingletonAutoService`,
`IsMultipleAttribute`, `ReplaceAutoServiceAttribute`, `CKTypeDefinerAttribute` and `CKTypeSuperDefinerAttribute`.

CK.Core defines also defines what is required to declare endpoint services:
`EndpointScopedServiceAttribute`, `EndpointSingletonServiceAttribute` and `IEndpointUbiquitousServiceDefault<out T>`.

See https://github.com/Invenietis/CK-Core/tree/develop/CK.Core/AutomaticDI


"Process context" for services and marshalling are not finalized yet: they are currently defined here.





