# Automatic DI

This folder contains type that will eventually be defined in CK.Core.
"Front/Process context" for services and marshalling is not finalized yet: we should be able to model
"endpoint adherence" of front services (there's more than one endpoint) and marshaller may not be able to work across
all contexts.

CK.Core defines: IRealObject, IAutoService, IScopedAutoService, ISingletonAutoService, IsMultipleAttribute, ReplaceAutoServiceAttribute,
CKTypeDefinerAttribute and CKTypeSuperDefinerAttribute.

See https://github.com/Invenietis/CK-Core/tree/develop/CK.Core/AutomaticDI

