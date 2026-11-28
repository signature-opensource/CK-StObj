using CK.CodeGen;
using CK.Core;
using CK.TypeSet;
using System;
using System.Linq;

namespace CK.Setup;

/// <summary>
/// The code generation of this engine: it completes the generated code of each App group, once the StObj engine's code
/// generation is done, before its signature, its save (<c>$StObjGen/G0.cs</c>) and its compilation. The generated
/// <c>CK.StObj.GeneratedRootContext</c> (an <see cref="IStObjMap"/>) gets its <see cref="ICKomposableMap"/> members.
/// </summary>
static class CKomposableMapGenerator
{
    internal static bool Generate( IActivityMonitor monitor, BinPathTypeGroup group, ICodeWorkspace workspace )
    {
        Throw.DebugAssert( !group.IsUnifiedPure );
        var root = workspace.Global.FindOrCreateNamespace( "CK.StObj" ).FindRequiredType( StObjContextRoot.RootContextTypeName );
        if( root == null )
        {
            return false;
        }
        // The values burnt in the code are part of the key of a group: its BinPaths have the same ones.
        GenerateCompiledConfiguration( monitor, group.Configurations[0], root );
        return true;
    }

    /// <summary>
    /// The <see cref="ICKomposableMap.CompiledConfiguration"/>: the values burnt by the App builder in the BinPath
    /// (<see cref="BinPathConfiguration.CompiledValues"/> and <see cref="BinPathConfiguration.CompiledLocks"/>), as
    /// literals.
    /// </summary>
    static void GenerateCompiledConfiguration( IActivityMonitor monitor, BinPathConfiguration configuration, ITypeScope root )
    {
        root.NewLine()
            .Append( "static readonly global::CK.Core.CompiledConfiguration _compiledConfiguration = " );
        if( configuration.CompiledValues.Count == 0 && configuration.CompiledLocks.Count == 0 )
        {
            root.Append( "global::CK.Core.CompiledConfiguration.Empty;" ).NewLine();
        }
        else
        {
            root.Append( "new global::CK.Core.CompiledConfiguration( new global::System.Collections.Generic.KeyValuePair<string, string>[] {" ).NewLine();
            foreach( var (path, value) in configuration.CompiledValues.OrderBy( kv => kv.Key, StringComparer.OrdinalIgnoreCase ) )
            {
                root.Append( "new( " ).AppendSourceString( path ).Append( ", " ).AppendSourceString( value ).Append( " )," ).NewLine();
            }
            root.Append( "}, new string[] {" );
            foreach( var l in configuration.CompiledLocks.Order( StringComparer.OrdinalIgnoreCase ) )
            {
                root.AppendSourceString( l ).Append( ", " );
            }
            root.Append( "} );" ).NewLine();
        }
        root.Append( "public global::CK.Core.CompiledConfiguration CompiledConfiguration => _compiledConfiguration;" ).NewLine();
        if( configuration.CompiledValues.Count > 0 )
        {
            monitor.Info( $"{configuration.CompiledValues.Count} configuration value(s) burnt: '{configuration.CompiledValues.Keys.Order( StringComparer.OrdinalIgnoreCase ).Concatenate( "', '" )}'." );
        }
    }
}
