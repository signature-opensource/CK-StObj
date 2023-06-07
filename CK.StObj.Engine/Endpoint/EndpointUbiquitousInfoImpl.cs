using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Implements the EndpointTypeManager.
    /// </summary>
    public sealed class EndpointUbiquitousInfoImpl : CSCodeGeneratorType
    {
        public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
        {
            Debug.Assert( scope.FullName == "CK.Core.EndpointUbiquitousInfo_CK" );
            scope.Definition.Modifiers |= Modifiers.Sealed;

            // We build the mappings here. We only need it here.
            List<(Type, int)> mappings = BuildMappings( c.CurrentRun.EngineMap );


            scope.GeneratedByComment( "Static constructor" )
                 .Append( "static EndpointUbiquitousInfo_CK()" )
                 .OpenBlock()
                 .Append( "_entries = new Entry[] {" )
                 .CreatePart( out var entries )
                 .Append( "};" ).NewLine()
                 .Append( "_descriptors = new Microsoft.Extensions.DependencyInjection.ServiceDescriptor[] {" )
                 .CreatePart( out var descriptors )
                 .Append( "};" ).NewLine()
                 .CloseBlock();

            foreach( var (type, index) in mappings )
            {
                entries.Append( "new Entry( " ).AppendTypeOf( type ).Append( ", " ).Append( index ).Append( " )," ).NewLine();
                descriptors.Append( "new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( " )
                           .AppendTypeOf( type )
                           .Append( ", sp => UntypedScopeDataHolder.GetUbiquitous( sp, " ).Append(index)
                           .Append( " ), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped )," ).NewLine();
            }


            scope.GeneratedByComment( "Constructor initializer" )
                 .Append( "protected override Mapper[] Initialize( IServiceProvider services )" )
                 .OpenBlock()
                 .Append( "return new Mapper[] {" );
            int current = -1;
            foreach( var (type, index) in mappings )
            {
                if( current == index ) continue;
                current = index;
                scope.Append( "new Mapper( Required( services, " ).AppendTypeOf( type ).Append( " ) )," ).NewLine();
            }
            scope.Append( "};" ).NewLine()
                 .CloseBlock();

            scope.Append( """
                           internal object At( int index ) => _mappers[index];

                           EndpointUbiquitousInfo_CK( Mapper[] mappers ) : base( mappers ) { }

                           public override EndpointUbiquitousInfo CleanClone()
                           {
                               var c = (Mapper[])_mappers.Clone();
                               for( int i = 0; i < c.Length; i++ )
                               {
                                   ref var m = ref c[i];
                                   m.Current = m.Initial;
                               }
                               return new EndpointUbiquitousInfo_CK( c );
                           }
                           
                           """ );

            return CSCodeGenerationResult.Success;
        }

        static List<(Type, int)> BuildMappings( IStObjEngineMap engineMap )
        {
            // Use list and not hash set (no volume here).
            var ubiquitousTypes = new List<Type>( engineMap.EndpointResult.UbiquitousInfoServices );
            var mappings = new List<(Type, int)>();
            int current = 0;
            while( ubiquitousTypes.Count > 0 )
            {
                var t = ubiquitousTypes[ubiquitousTypes.Count - 1];
                var auto = engineMap.Services.ToLeaf( t );
                if( auto != null )
                {
                    // We (heavily) rely on the fact that the UniqueMappings are ordered
                    // from most abstract to leaf type here.
                    foreach( var m in auto.UniqueMappings )
                    {
                        mappings.Add( (m, current) );
                        ubiquitousTypes.Remove( m );
                    }
                }
                else
                {
                    mappings.Add( (t, current) );
                    ubiquitousTypes.RemoveAt( ubiquitousTypes.Count - 1 );
                }
                ++current;
            }
            return mappings;
        }
    }
}
