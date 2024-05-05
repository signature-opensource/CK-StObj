using CK.Core;
using System;
using System.Collections.Immutable;

namespace CK.StObj.Engine.Tests.Endpoint.Conformant
{
    sealed class AmbientServiceHub_CK : CK.Core.AmbientServiceHub
    {
        public AmbientServiceHub_CK( IServiceProvider services ) : base( services )
        {
        }

        protected override Mapper[] Initialize( IServiceProvider services, out ImmutableArray<DIContainerHub.AmbientServiceMapping> entries )
        {
            entries = DIContainerHub_CK._ambientMappings;
            return new Mapper[] {
                default, // AmbientServiceHub
                new Mapper( Required( services, typeof(IFakeTenantInfo) ) ),
                new Mapper( Required( services, typeof(IFakeAuthenticationInfo) ) ),
                new Mapper( Required( services, typeof(FakeAuthenticationInfo) ) ),
                new Mapper( Required( services, typeof(FakeCultureInfo) ) )
            };

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
            var c = (Mapper[])_mappers.Clone();
            for( int i = 0; i < c.Length; i++ )
            {
                ref var m = ref c[i];
                if( restoreInitialValues ) m.Current = m.Initial;
                else m.Initial = m.Current;
            }
            return new AmbientServiceHub_CK( c );
        }
    }

}
