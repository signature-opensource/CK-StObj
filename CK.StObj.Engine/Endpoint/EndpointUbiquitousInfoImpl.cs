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
            var mappings = c.CurrentRun.EngineMap.EndpointResult.UbiquitousMappings;

            scope.Append( "internal static Microsoft.Extensions.DependencyInjection.ServiceDescriptor[] _descriptors;" ).NewLine();

            scope.GeneratedByComment( "Static constructor" )
                 .Append( "static EndpointUbiquitousInfo_CK()" )
                 .OpenBlock()
                 .Append( "_entries = EndpointTypeManager_CK._ubiquitousMappings;" ).NewLine()
                 .Append( "_descriptors = new Microsoft.Extensions.DependencyInjection.ServiceDescriptor[] {" )
                 .CreatePart( out var descriptors )
                 .Append( "};" ).NewLine()
                 .CloseBlock();

            foreach( var (type, index) in mappings )
            {
                descriptors.Append( "new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( " )
                           .AppendTypeOf( type )
                           .Append( ", sp => CK.StObj.ScopeDataHolder.GetUbiquitous( sp, " ).Append(index)
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
                 .Append( """
                          static object Required( IServiceProvider services, Type type )
                          {
                              var o = services.GetService( type );
                              if( o != null ) return o;
                              return Throw.InvalidOperationException<object>( $"Ubiquitous service '{type}' not registered! This type must always be resolvable." );
                          }
                          """ )
               .CloseBlock();

            scope.Append( """
                           internal object At( int index ) => _mappers[index].Current;

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
    }
}
