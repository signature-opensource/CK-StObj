using CK.Core;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;

namespace CK.StObj.Engine.Tests.Endpoint.Conformant
{
    sealed class EndpointUbiquitousInfo_CK : CK.Core.EndpointUbiquitousInfo
    {
        public EndpointUbiquitousInfo_CK( IServiceProvider services ) : base( services )
        {
        }

        // Descriptors for back endpoints: ubiquitous informations are resolved from the
        // EndpointUbiquitousInfo scoped instance.
        internal static Microsoft.Extensions.DependencyInjection.ServiceDescriptor[] _descriptors;

        static EndpointUbiquitousInfo_CK()
        {
            // IFakeTenantInfo is a IAutoService: its specialization chain entries
            // are listed that points to the same Mapper (here the first one).
            // This is not the case of IFakeAuthenticationInfo/FakeAuthenticationInfo: these are
            // de facto 2 independent services (standard DI behavior).
            _entries = EndpointTypeManager_CK._ubiquitousMappings;
            _descriptors = new Microsoft.Extensions.DependencyInjection.ServiceDescriptor[] {
                new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( typeof(IFakeTenantInfo), sp => ScopeDataHolder.GetUbiquitous( sp, 0 ), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped ),
                new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( typeof(FakeTenantInfo), sp => ScopeDataHolder.GetUbiquitous( sp, 0 ), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped ),
                new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( typeof(IFakeAuthenticationInfo), sp => ScopeDataHolder.GetUbiquitous( sp, 1 ), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped ),
                new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( typeof(FakeAuthenticationInfo), sp => ScopeDataHolder.GetUbiquitous( sp, 2 ), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped ),
                new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( typeof(FakeCultureInfo), sp => ScopeDataHolder.GetUbiquitous( sp, 3 ), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped ),
            };
        }

        protected override Mapper[] Initialize( IServiceProvider services )
        {
            return new Mapper[]
            {
                new Mapper( Required( services, typeof(IFakeTenantInfo) ) ),
                new Mapper( Required( services, typeof(IFakeAuthenticationInfo) ) ),
                new Mapper( Required( services, typeof(FakeAuthenticationInfo) ) ),
                new Mapper( Required( services, typeof(FakeCultureInfo) ) ),
            };

            static object Required( IServiceProvider services, Type type )
            {
                var o = services.GetService( type );
                if( o != null ) return o;
                return Throw.InvalidOperationException<object>( $"Ubiquitous service '{type.ToCSharpName()}' not registered! This type must always be resolvable." );
            }
        }

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
    }

}
