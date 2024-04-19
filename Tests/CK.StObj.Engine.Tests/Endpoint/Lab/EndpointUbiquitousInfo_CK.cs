using CK.Core;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Immutable;

namespace CK.StObj.Engine.Tests.Endpoint.Conformant
{
    sealed class AmbientServiceHub_CK : CK.Core.AmbientServiceHub
    {
        public AmbientServiceHub_CK( IServiceProvider services ) : base( services )
        {
        }

        // Descriptors for back endpoints: ubiquitous informations are resolved from the
        // AmbientServiceHub scoped instance.
        internal static Microsoft.Extensions.DependencyInjection.ServiceDescriptor[] _descriptors;

        static AmbientServiceHub_CK()
        {
            // IFakeTenantInfo is a IAutoService: its specialization chain entries
            // are listed that points to the same Mapper (here the first one).
            // This is not the case of IFakeAuthenticationInfo/FakeAuthenticationInfo: these are
            // de facto 2 independent services (standard DI behavior).
            _descriptors = new Microsoft.Extensions.DependencyInjection.ServiceDescriptor[] {
                new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( typeof(IFakeTenantInfo), sp => ScopeDataHolder.GetAmbientService( sp, 0 ), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped ),
                new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( typeof(FakeTenantInfo), sp => ScopeDataHolder.GetAmbientService( sp, 0 ), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped ),
                new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( typeof(IFakeAuthenticationInfo), sp => ScopeDataHolder.GetAmbientService( sp, 1 ), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped ),
                new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( typeof(FakeAuthenticationInfo), sp => ScopeDataHolder.GetAmbientService( sp, 2 ), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped ),
                new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( typeof(FakeCultureInfo), sp => ScopeDataHolder.GetAmbientService( sp, 3 ), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped ),
            };
        }

        protected override Mapper[] Initialize( IServiceProvider services, out ImmutableArray<DIContainerHub.AmbientServiceMapping> entries )
        {
            entries = DIContainerHub_CK._ambientMappings;
            return new Mapper[] {
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

        public override AmbientServiceHub CleanClone()
        {
            var c = (Mapper[])_mappers.Clone();
            for( int i = 0; i < c.Length; i++ )
            {
                ref var m = ref c[i];
                m.Current = m.Initial;
            }
            return new AmbientServiceHub_CK( c );
        }
    }

}
