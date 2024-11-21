using CK.CodeGen;
using System;
using CK.Core;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace CK.Setup;


/// <summary>
/// Implements a DIContainerDefinition instance.
/// There is only the Name and the Kind to generate here...
/// </summary>
public sealed class DIContainerDefinitionImpl : CSCodeGeneratorType, IAttributeContextBound
{
    readonly DIContainerDefinitionAttribute _attr;

    /// <summary>
    /// Initializes a new endpoint definition implementor.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="decorated">The <see cref="DIContainerDefinition"/> type.</param>
    /// <param name="attr">This attribute.</param>
    public DIContainerDefinitionImpl( IActivityMonitor monitor, Type decorated, DIContainerDefinitionAttribute attr )
    {
        DIContainerInfo.CheckEndPointDefinition( monitor, decorated );
        _attr = attr;
    }

    /// <summary>
    /// Gets the <see cref="DIContainerDefinitionAttribute.Kind"/>.
    /// </summary>
    public DIContainerKind Kind => _attr.Kind;

    /// <inheritdoc />
    public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
    {
        if( c.ActualSourceCodeIsUseless ) return CSCodeGenerationResult.Success;

        scope.Definition.Modifiers |= Modifiers.Sealed;

        var endpointResult = c.CurrentRun.EngineMap.EndpointResult;
        var e = endpointResult.Containers.First( c => c.DIContainerDefinition.ClassType == classType );

        scope.Append( "public override string Name => " ).AppendSourceString( e.Name ).Append( ";" ).NewLine()
             .Append( "public override DIContainerKind Kind => DIContainerKind." ).Append( _attr.Kind.ToString() ).Append( ";" ).NewLine();

        return CSCodeGenerationResult.Success;
    }
}
