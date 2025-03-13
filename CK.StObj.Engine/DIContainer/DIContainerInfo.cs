using CK.Core;
using System;

namespace CK.Setup;

/// <summary>
/// Describes a <see cref="DIContainerDefinition"/>.
/// </summary>
public sealed class DIContainerInfo : IDIContainerInfo
{
    readonly IStObjResult _endpointDefinition;
    readonly string _name;
    readonly DIContainerKind _kind;
    readonly Type? _instanceDataType;

    internal DIContainerInfo( IStObjResult endpointDefinition, string name, DIContainerKind kind, Type? instanceDataType )
    {
        _name = name;
        _kind = kind;
        _instanceDataType = instanceDataType;
        _endpointDefinition = endpointDefinition;
    }

    /// <inheritdoc />
    public IStObjResult DIContainerDefinition => _endpointDefinition;

    /// <inheritdoc />
    public string Name => _name;

    /// <inheritdoc />
    public Type? ScopeDataType => _instanceDataType;

    /// <inheritdoc />
    public DIContainerKind Kind => _kind;

    internal static ReadOnlySpan<char> DefinitionName( Type definition ) => definition.Name.AsSpan( 0, definition.Name.Length - 21 );

    internal static bool CheckEndPointDefinition( IActivityMonitor monitor, Type t )
    {
        var b = t.BaseType;
        if( b == null || !b.IsGenericType || b.GetGenericTypeDefinition() != typeof( DIContainerDefinition<> ) )
        {
            monitor.Error( $"DIContainerDefinition type '{t:C}' must directly specialize DIContainerDefinition<TScopeData> (not '{b:C}')." );
            return false;
        }
        var n = t.Name;
        if( n.Length <= 21 || !n.EndsWith( "DIContainerDefinition", StringComparison.Ordinal ) )
        {
            monitor.Error( $"Invalid DIContainerDefinition type '{t:C}': DIContainerDefinition type name must end with \"DIContainerDefinition\" (the prefix becomes the container name)." );
            return false;
        }
        return true;
    }


}
