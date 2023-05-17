using CK.Core;
using System;

namespace CK.Setup
{
    public sealed partial class CKTypeEndpointServiceInfo
    {
        internal static ReadOnlySpan<char> DefinitionName( Type definition ) => definition.Name.AsSpan( 0, definition.Name.Length - 18 );

        internal static bool CheckEndPointDefinition( IActivityMonitor monitor, Type t )
        {
            if( t.BaseType != typeof( EndpointDefinition ) )
            {
                monitor.Error( $"EndpointDefinition type '{t:C}' must directly specialize EndpointDefinition (base type is '{t.BaseType:C}')." );
                return false;
            }
            var n = t.Name;
            if( n.Length <= 18 || !n.EndsWith( "EndpointDefinition", StringComparison.Ordinal ) )
            {
                monitor.Error( $"Invalid EndpointDefinition type '{t:C}': EndpointDefinition type name must end with \"EndpointDefinition\" (the prefix becomes the simple endpoint name)." );
                return false;
            }
            return true;
        }

    }

}
