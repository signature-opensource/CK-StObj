using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Implements the DIContainerHub.
    /// </summary>
    public sealed class AmbientServiceHubImpl : CSCodeGeneratorType
    {
        /// <inheritdoc />
        public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
        {
            Debug.Assert( scope.FullName == "CK.Core.AmbientServiceHub_CK" );
            scope.Definition.Modifiers |= Modifiers.Sealed;

            var mappings = c.CurrentRun.EngineMap.EndpointResult.AmbientServiceMappings;

            scope.GeneratedByComment( "Constructor initializer" )
                 .Append( "protected override Mapper[] Initialize( IServiceProvider services, out ImmutableArray<DIContainerHub.AmbientServiceMapping> entries )" )
                 .OpenBlock()
                 .Append( "entries = DIContainerHub_CK._ambientMappings;" ).NewLine()
                 .Append( "return new Mapper[] {" ).NewLine();
            int current = -1;
            foreach( var (type, index) in mappings )
            {
                if( current == index ) continue;
                current = index;
                scope.Append( "new Mapper( Required( services, " ).AppendTypeOf( type ).Append( " ) )," ).NewLine();
            }
            scope.Append( "};" ).NewLine()
                 .Append( """
                          static object Required( IServiceProvider services, Type type )
                          {
                              var o = services.GetService( type );
                              if( o != null ) return o;
                              return Throw.InvalidOperationException<object>( $"Ambient service '{type}' not registered! This type must always be resolvable." );
                          }
                          """ )
               .CloseBlock();

            scope.Append( """
                           internal object At( int index ) => _mappers[index].Current;

                           AmbientServiceHub_CK( Mapper[] mappers ) : base( mappers, DIContainerHub_CK._ambientMappings ) { }

                           public override AmbientServiceHub CleanClone( bool restoreInitialValues = false )
                           {
                               var c = (Mapper[])_mappers.Clone();
                               for( int i = 0; i < c.Length; i++ )
                               {
                                   ref var m = ref c[i];
                                   if( restoreInitialValues ) m.Current = m.Initial;
                                   else m.Initial = m.Current;
                               }
                               return new AmbientServiceHub_CK( c );
                           }
                           
                           """ );

            return CSCodeGenerationResult.Success;
        }
    }
}
