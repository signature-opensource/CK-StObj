namespace CK.Setup
{
    public sealed partial class CachedType
    {
        const int _privateStartKind = 4096;

        /// <summary>
        /// Mask for public information defined in the <see cref="CKTypeKind"/> enumeration.
        /// Internally other flags are used.
        /// </summary>
        internal const CKTypeKind MaskPublicInfo = (CKTypeKind)(_privateStartKind - 1);

        internal const CKTypeKind IsDefiner = (CKTypeKind)_privateStartKind;

        internal const CKTypeKind IsSuperDefiner = (CKTypeKind)(_privateStartKind << 1);

        // The lifetime reason is the interface marker (applies to all our marker interfaces).
        internal const CKTypeKind IsReasonMarker = (CKTypeKind)(_privateStartKind << 2);

        // The type is a service that is scoped because its ctor references a scoped service.
        internal const CKTypeKind IsScopedReasonReference = (CKTypeKind)(_privateStartKind << 3);

        // The service is Marshallable because a IAutoService Marshaller class has been found.
        internal const CKTypeKind IsMarshallableReasonMarshaller = (CKTypeKind)(_privateStartKind << 4);

        // The lifetime reason is an external definition (applies to IsSingleton and IsScoped).
        internal const CKTypeKind IsLifetimeReasonExternal = (CKTypeKind)(_privateStartKind << 5);

        // The IsProcessService reason is an external definition.
        internal const CKTypeKind IsProcessServiceReasonExternal = (CKTypeKind)(_privateStartKind << 6);

        // The IsEndpoint reason is an external definition.
        internal const CKTypeKind IsEndpointServiceReasonExternal = (CKTypeKind)(_privateStartKind << 7);

        // The IsMultiple reason is an external definition.
        internal const CKTypeKind IsMultipleReasonExternal = (CKTypeKind)(_privateStartKind << 8);

        internal static string ToStringFull( CKTypeKind t )
        {
            var c = (t & MaskPublicInfo).ToStringFlags();
            if( (t & IsDefiner) != 0 ) c += " [IsDefiner]";
            if( (t & IsSuperDefiner) != 0 ) c += " [IsSuperDefiner]";
            if( (t & IsReasonMarker) != 0 ) c += " [IsMarkerInterface]";
            if( (t & IsLifetimeReasonExternal) != 0 ) c += " [Lifetime:External]";
            if( (t & IsScopedReasonReference) != 0 ) c += " [Lifetime:UsesScoped]";
            if( (t & IsMarshallableReasonMarshaller) != 0 ) c += " [Marshallable:MarshallerExists]";
            if( (t & IsProcessServiceReasonExternal) != 0 ) c += " [ProcessService:External]";
            if( (t & IsMultipleReasonExternal) != 0 ) c += " [Multiple:External]";
            return c;
        }
    }

}
