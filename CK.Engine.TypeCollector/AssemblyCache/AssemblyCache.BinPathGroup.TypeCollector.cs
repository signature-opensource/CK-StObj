using CK.Core;
using CK.Setup;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System;

namespace CK.Engine.TypeCollector;


public sealed partial class AssemblyCache // BinPathGroup.TypeCollector
{
    public sealed partial class BinPathGroup
    {
        internal bool FinalizeAndCollectTypes( IActivityMonitor monitor, GlobalTypeCache typeCache, byte[] hashExternalTypes )
        {
            // Builds the GroupName.
            if( _configurations.Count > 1 )
            {
                _configurations.Sort( ( a, b ) => a.Name.CompareTo( b.Name ) );
                _groupName = _configurations.Select( b => b.Name ).Concatenate( "-" );
            }
            monitor.Trace( $"Skipped {_systemSkipped.Count} system assemblies: {_systemSkipped.Select( a => a.Name ).Concatenate()}." );
            // Useless to keep the list content.
            _systemSkipped.Clear();
            if( !_success )
            {
                monitor.Trace( $"BinPathGroup '{_groupName}' is on error." );
                return false;
            }
            using var _ = monitor.OpenInfo( $"Collecting types for BinPathGroup '{_groupName}' from head PFeatures: '{_heads.Keys.Select( a => a.Name ).Concatenate( "', '" )}'." );
            using var hasher = IncrementalHash.CreateHash( HashAlgorithmName.SHA1 );
            hasher.Append( _path.Path );
            hasher.AppendData( hashExternalTypes );

            var c = new HashSet<ICachedType>();
            bool success = true;
            foreach( var head in _heads.Keys )
            {
                head.AddHash( hasher );
                success &= CollectTypes( monitor, typeCache, head, out var headC );
                c.UnionWith( headC );
            }
            _signature = new SHA1Value( hasher, resetHasher: false );
            if( success ) _result = c;
            return success;

            static bool CollectTypes( IActivityMonitor monitor, GlobalTypeCache typeCache, CachedAssembly assembly, out HashSet<ICachedType> c )
            {
                Throw.DebugAssert( assembly.IsInitialAssembly && !assembly.Kind.IsSkipped() );
                if( assembly._types != null )
                {
                    c = assembly._types;
                    return true;
                }
                c = new HashSet<ICachedType>();
                var assemblySourceName = assembly.ToString();
                using var _ = monitor.OpenInfo( assemblySourceName );
                bool success = true;
                foreach( var sub in assembly.PFeatures )
                {
                    success &= CollectTypes( monitor, typeCache, sub, out var subC );
                    c.UnionWith( subC );
                }
                // Consider the visible classes, interfaces, value types and enums excluding any generic type definitions.
                // These are the only kind of types that we need to start a CKomposable setup.
                c.AddRange( assembly.Assembly.GetExportedTypes()
                                             .Where( t => (t.IsClass || t.IsInterface || t.IsValueType || t.IsEnum) && !t.IsGenericTypeDefinition )
                                             .Select( typeCache.Get ) );
                // Don't merge the 2 loops here!
                // We must first handle the Add and then the Remove.
                // 1 - Add types.
                List<ICachedType>? changed = null;
                foreach( var a in assembly.CustomAttributes )
                {
                    if( a.AttributeType == typeof( RegisterCKTypeAttribute ) )
                    {
                        var ctorArgs = a.ConstructorArguments;
                        // Constructor (Type, Type[] others):
                        if( ctorArgs[1].Value is Type?[] others )
                        {
                            // Filters out null thanks to "is".
                            if( ctorArgs[0].Value is Type t )
                            {
                                success &= HandleType( monitor, typeCache, c, ref changed, add: true, assemblySourceName, t );
                            }
                            // Maximal precautions: filters out any potential null.
                            foreach( var o in others )
                            {
                                if( o == null ) continue;
                                success &= HandleType( monitor, typeCache, c, ref changed, add: true, assemblySourceName, o );
                            }
                        }
                    }
                }
                if( success && changed != null )
                {
                    monitor.Info( $"Assembly '{assembly.Name}' explicitly registers {changed.Count} types: '{changed.Select( t => t.CSharpName ).Concatenate( "', '" )}'." );
                    changed.Clear();
                }
                // 2 - Remove types.
                foreach( var a in assembly.CustomAttributes )
                {
                    if( a.AttributeType == typeof( CK.Setup.ExcludeCKTypeAttribute ) )
                    {
                        var ctorArgs = a.ConstructorArguments;
                        if( ctorArgs[0].Value is Type t )
                        {
                            success &= HandleType( monitor, typeCache, c, ref changed, add: false, assemblySourceName, t );
                        }
                        if( ctorArgs[1].Value is Type?[] others && others.Length > 0 )
                        {
                            foreach( var o in others )
                            {
                                if( o == null ) continue;
                                success &= HandleType( monitor, typeCache, c, ref changed, add: false, assemblySourceName, o );
                            }
                        }
                    }
                }
                if( success && changed != null )
                {
                    monitor.Info( $"Assembly '{assembly.Name}' explicitly removed {changed.Count} types from registration: '{changed.Select( t => t.CSharpName ).Concatenate( "', '" )}'." );
                }
                monitor.CloseGroup( $"{c.Count} types." );
                assembly._types = c;
                return success;

                static bool HandleType( IActivityMonitor monitor,
                                        GlobalTypeCache typeCache,
                                        HashSet<ICachedType> c,
                                        ref List<ICachedType>? changed,
                                        bool add,
                                        string sourceAssemblyName,
                                        Type t )
                {
                    var error = CachedType.ComputeUnhandledTypeKind( t, null ).GetUnhandledMessage();
                    if( error != null )
                    {
                        monitor.Error( $"Invalid [assembly:{(add ? "Register" : "Exclude")}CKTypeAttribute] in {sourceAssemblyName}: type '{t:N}' {error}." );
                        return false;
                    }
                    var cT = typeCache.Get( t );
                    if( add ? c.Add( cT ) : c.Remove( cT ) )
                    {
                        changed ??= new List<ICachedType>();
                        changed.Add( cT );
                    }
                    return true;
                }
            }
        }

        internal static string? GetConfiguredTypeErrorMessage( GlobalTypeCache typeCache, ICachedType type, ExternalServiceKind kind )
        {
            Throw.DebugAssert( kind != ExternalServiceKind.None );
            var msg = type.Kind.GetUnhandledMessage();
            if( msg == null )
            {
                string? k = null;
                if( type.Interfaces.Contains( typeCache.IAutoService ) )
                {
                    k = nameof( IAutoService );
                }
                else if( type is IRealObjectCachedType )
                {
                    k = nameof( IRealObject );
                }
                else if( type is IPocoCachedType )
                {
                    k = nameof( IPoco );
                }
                if( k != null )
                {
                    return $"is a {k}. IAutoService, IRealObject and IPoco cannot be externally configured";
                }
            }
            return msg;
        }

    }

}
