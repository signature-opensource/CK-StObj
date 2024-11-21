using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace CK.Setup;

/// <summary>
/// Implements the DIContainerHub.
/// </summary>
public sealed class AmbientServiceHubImpl : CSCodeGeneratorType
{
    /// <inheritdoc />
    public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
    {
        Throw.DebugAssert( scope.FullName == "CK.Core.AmbientServiceHub_CK" );
        scope.Definition.Modifiers |= Modifiers.Sealed;
        scope.GeneratedByComment()
             .Append( """
            static Mapper[]? _default;

            // Available to generated code: an unlocked hub with the default values of all ambient services.
            public AmbientServiceHub_CK() : base( GetDefault(), DIContainerHub_CK._ambientMappings )
            {
            }

            AmbientServiceHub_CK( Mapper[] mappers ) : base( mappers, DIContainerHub_CK._ambientMappings )
            {
            }

            protected override Mapper[] Initialize( IServiceProvider services, out ImmutableArray<DIContainerHub.AmbientServiceMapping> entries )
            {
                entries = DIContainerHub_CK._ambientMappings;
                return BuildFrom( services );
            }

            internal object At( int index ) => _mappers[index].Current;

            public override AmbientServiceHub CleanClone( bool restoreInitialValues = false )
            {
                var c = System.Runtime.CompilerServices.Unsafe.As<Mapper[]>( _mappers.Clone() );
                if( restoreInitialValues )
                    for( int i = 0; i < c.Length; i++ )
                    {
                        ref var m = ref c[i];
                        m.Current = m.Initial;
                    }
                else
                    for( int i = 0; i < c.Length; i++ )
                    {
                        ref var m = ref c[i];
                        m.Initial = m.Current;
                    }
                return new AmbientServiceHub_CK( c );
            }
                       
            """ );

        var endpointResult = c.CurrentRun.EngineMap.EndpointResult;

        var mappings = endpointResult.AmbientServiceMappings;
        scope.Append( "static Mapper[] BuildFrom( IServiceProvider services )" )
             .OpenBlock()
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
                          return o ?? Throw.InvalidOperationException<object>( $"Ambient service '{type}' not registered! This type must always be resolvable." );
                      }
                      """ )
           .CloseBlock();

        scope.Append( "static Mapper[] GetDefault()" )
             .OpenBlock()
             .Append( "// Don't care of race conditions here." ).NewLine()
             .Append( "_default ??= new Mapper[] {" ).NewLine();
        current = -1;
        foreach( var (type, index) in mappings )
        {
            if( current == index ) continue;
            current = index;
            var defaultProvider = endpointResult.DefaultAmbientServiceValueProviders[index];
            scope.Append( "new Mapper( ((" )
                    .AppendGlobalTypeName( defaultProvider.ProviderType )
                    .Append( "?)DIContainerHub_CK.GlobalServices.GetService( " ).AppendTypeOf( defaultProvider.Provider.ClassType )
                    .Append( " )!).Default )," ).NewLine();
        }
        scope.Append( "};" ).NewLine()
             .Append( "return System.Runtime.CompilerServices.Unsafe.As<Mapper[]>( _default.Clone() );" ).NewLine()
             .CloseBlock();

        return CSCodeGenerationResult.Success;
    }
}
