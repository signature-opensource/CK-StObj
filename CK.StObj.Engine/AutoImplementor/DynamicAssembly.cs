using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using CK.CodeGen;
using CK.Core;

#nullable enable

namespace CK.Setup;

/// <summary>
/// Implements <see cref="IDynamicAssembly"/>.
/// </summary>
public class DynamicAssembly : IDynamicAssembly
{
    int _typeID;
    readonly Dictionary<object, object?> _memory;

    /// <summary>
    /// Initializes a new <see cref="DynamicAssembly"/>.
    /// </summary>
    /// <param name="code">Optional code workspace (a new one by default).</param>
    public DynamicAssembly( ICodeWorkspace? code = null )
    {
        var name = Guid.NewGuid().ToString();
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly( new AssemblyName( name ), AssemblyBuilderAccess.Run );
        StubModuleBuilder = assemblyBuilder.DefineDynamicModule( name );

        _memory = new Dictionary<object, object?>();
        Code = code ?? CodeWorkspace.Create();
        Throw.DebugAssert( typeof( StObjGenAttribute ).FullName == "CK.Core.StObjGenAttribute" );
        Code.TypeCreated += t => t.Definition.Attributes.Ensure( CodeAttributeTarget.Type ).Attributes.Add( new AttributeDefinition( "CK.Core.StObjGen" ) );
    }

    /// <inheritdoc />
    public Dictionary<object, object?> Memory => _memory;

    /// <inheritdoc />
    public ModuleBuilder StubModuleBuilder { get; }

    /// <inheritdoc />
    public ICodeWorkspace Code { get; }

    /// <inheritdoc />
    public string NextUniqueNumber() => (++_typeID).ToString();

}
