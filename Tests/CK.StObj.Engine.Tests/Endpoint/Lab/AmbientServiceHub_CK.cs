using CK.Core;
using System;
using System.Collections.Immutable;

namespace CK.StObj.Engine.Tests.Endpoint.Conformant;

sealed class AmbientServiceHub_CK : CK.Core.AmbientServiceHub
{
    static Mapper[]? _default;

    static Mapper[] GetDefault()
    {
        // Don't care of race conditions here.
        _default ??= [
            new Mapper( ((IAmbientServiceDefaultProvider<FakeAuthenticationInfo>?)DIContainerHub_CK.GlobalServices.GetService( typeof(DefaultAuthenticationInfoProvider) )!).Default ),
            new Mapper( ((IAmbientServiceDefaultProvider<IFakeAuthenticationInfo>?)DIContainerHub_CK.GlobalServices.GetService( typeof(DefaultAuthenticationInfoProvider) )!).Default ),
            new Mapper( ((IAmbientServiceDefaultProvider<FakeCultureInfo>?)DIContainerHub_CK.GlobalServices.GetService( typeof(DefaultCultureProvider) )!).Default ),
            new Mapper( ((IAmbientServiceDefaultProvider<IFakeTenantInfo>?)DIContainerHub_CK.GlobalServices.GetService( typeof(DefaultTenantProvider) )!).Default ),
        ];
        return System.Runtime.CompilerServices.Unsafe.As<Mapper[]>( _default.Clone() );

    }

    // Available to generated code: an unlocked hub with the default values of all ambient services.
    public AmbientServiceHub_CK()
        : base( GetDefault(), DIContainerHub_CK._ambientMappings )
    {
    }

    public AmbientServiceHub_CK( IServiceProvider services ) : base( services )
    {
    }

    protected override Mapper[] Initialize( IServiceProvider services, out ImmutableArray<DIContainerHub.AmbientServiceMapping> entries )
    {
        entries = DIContainerHub_CK._ambientMappings;
        return BuildFrom( services );
    }

    static Mapper[] BuildFrom( IServiceProvider services )
    {
        return [
            new Mapper( Required( services, typeof(IFakeTenantInfo) ) ),
            new Mapper( Required( services, typeof(IFakeAuthenticationInfo) ) ),
            new Mapper( Required( services, typeof(FakeAuthenticationInfo) ) ),
            new Mapper( Required( services, typeof(FakeCultureInfo) ) )
        ];

        static object Required( IServiceProvider services, Type type )
        {
            var o = services.GetService( type );
            if( o != null ) return o;
            return Throw.InvalidOperationException<object>( $"Ambient service '{type.ToCSharpName()}' not registered! This type must always be resolvable." );
        }
    }

    internal object At( int index ) => _mappers[index].Current;

    AmbientServiceHub_CK( Mapper[] mappers ) : base( mappers, DIContainerHub_CK._ambientMappings ) { }

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
}
