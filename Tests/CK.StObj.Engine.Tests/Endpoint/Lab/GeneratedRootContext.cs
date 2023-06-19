using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace CK.StObj.Engine.Tests.Endpoint.Conformant
{
    // This class is generated.
    sealed class StObjServiceClassDescriptor : IStObjServiceClassDescriptor
    {
        public StObjServiceClassDescriptor( Type t, Type finalType, AutoServiceKind k, IReadOnlyCollection<Type> marshallableTypes, IReadOnlyCollection<Type> mult, IReadOnlyCollection<Type> uniq )
        {
            ClassType = t;
            FinalType = finalType;
            AutoServiceKind = k;
            MarshallableTypes = marshallableTypes;
            MultipleMappings = mult;
            UniqueMappings = uniq;
        }

        public Type ClassType { get; }

        public Type FinalType { get; }

        public bool IsScoped => (AutoServiceKind & AutoServiceKind.IsScoped) != 0;

        public AutoServiceKind AutoServiceKind { get; }

        public IReadOnlyCollection<Type> MarshallableTypes { get; }

        public IReadOnlyCollection<Type> MultipleMappings { get; }

        public IReadOnlyCollection<Type> UniqueMappings { get; }
    }


    sealed class GeneratedRootContext : IStObjMap, IStObjObjectMap, IStObjServiceMap
    {
        static readonly Dictionary<Type, IStObjServiceClassDescriptor> _serviceMappings;

        static GeneratedRootContext()
        {
            // For this sample we only need those 3 ServiceClassDescriptor to
            // be able to resolve the default value of ubiquitous info services for front endpoints.
            // In real life, there would be the other auto services here.
            _serviceMappings = new Dictionary<Type, IStObjServiceClassDescriptor>()
            {
                { typeof(DefaultTenantProvider), new StObjServiceClassDescriptor( typeof(DefaultTenantProvider),
                                                                                  typeof(DefaultTenantProvider),
                                                                                  AutoServiceKind.IsAutoService | AutoServiceKind.IsSingleton,
                                                                                  marshallableTypes: Type.EmptyTypes,
                                                                                  mult: Type.EmptyTypes,
                                                                                  uniq: Type.EmptyTypes) },
                { typeof(DefaultCultureProvider), new StObjServiceClassDescriptor( typeof(DefaultCultureProvider),
                                                                                   typeof(DefaultCultureProvider),
                                                                                   AutoServiceKind.IsAutoService | AutoServiceKind.IsSingleton,
                                                                                   marshallableTypes: Type.EmptyTypes,
                                                                                   mult: Type.EmptyTypes,
                                                                                   uniq: Type.EmptyTypes) },
                { typeof(DefaultAuthenticationInfoProvider), new StObjServiceClassDescriptor( typeof(DefaultAuthenticationInfoProvider),
                                                                                              typeof(DefaultAuthenticationInfoProvider),
                                                                                              AutoServiceKind.IsAutoService | AutoServiceKind.IsSingleton,
                                                                                              marshallableTypes: Type.EmptyTypes,
                                                                                              mult: Type.EmptyTypes,
                                                                                              uniq: Type.EmptyTypes) }
            };
        }

        public IStObjObjectMap StObjs => this;

        public SHA1Value GeneratedSignature => throw new NotImplementedException();

        public IStObjServiceMap Services => this;

        public IReadOnlyList<string> Names => throw new NotImplementedException();

        public IReadOnlyCollection<VFeature> Features => throw new NotImplementedException();

        public IReadOnlyDictionary<Type, IStObjMultipleInterface> MultipleMappings => ImmutableDictionary<Type,IStObjMultipleInterface>.Empty;

        IReadOnlyList<IStObjFinalImplementation> IStObjObjectMap.FinalImplementations => Array.Empty<IStObjFinalImplementation>();

        IEnumerable<StObjMapping> IStObjObjectMap.StObjs => throw new NotImplementedException();

        IReadOnlyDictionary<Type, IStObjFinalImplementation> IStObjServiceMap.ObjectMappings => throw new NotImplementedException();

        IReadOnlyList<IStObjFinalImplementation> IStObjServiceMap.ObjectMappingList => throw new NotImplementedException();

        IReadOnlyDictionary<Type, IStObjServiceClassDescriptor> IStObjServiceMap.Mappings => _serviceMappings;

        IStObjFinalImplementation? IStObjObjectMap.ToLeaf( Type t ) => throw new NotImplementedException();

        object? IStObjObjectMap.Obtain( Type t ) => throw new NotImplementedException();

        IReadOnlyList<IStObjServiceClassDescriptor> IStObjServiceMap.MappingList => Array.Empty<IStObjServiceClassDescriptor>();

        IStObjFinalClass? IStObjMap.ToLeaf( Type t ) => ToLeaf( t );

        IStObjFinalClass? IStObjServiceMap.ToLeaf( Type t ) => ToLeaf( t );

        internal static IStObjFinalClass? ToLeaf( Type t )
        {
            // This is not the real generated code.
            return _serviceMappings.GetValueOrDefault( t );
        }

        // This method is generated by code.
        // Note: When no EndpointDefinition exist, the code that manages endpoints is not generated.
        public bool ConfigureServices( in StObjContextRoot.ServiceRegister reg )
        {
            // Gives the real objects an opportunity to configure the services.
            RealObjectConfigureServices( in reg );

            // Check the ubiquitous services.
            if( !EndpointHelper.CheckAndNormalizeUbiquitousInfoServices( reg.Monitor, reg.Services, true ) )
            {
                return false;
            }

            // - We build a mapping of ServiceType -> ServiceDescriptors from the global configuration (only if there are endpoints).
            var mappings = EndpointHelper.CreateInitialMapping( reg.Monitor, reg.Services, EndpointTypeManager_CK._endpointServices.ContainsKey );

            // - We add the code generated HostedServiceLifetimeTrigger to the global container: the endpoint
            //   containers don't need it.
            //   We inject it at the 0 index: it will be the first one to be triggered.
            //   We don't do it here to avoid creating yet another fake implementation.
            //   
            // reg.Services.Insert( 0, new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( typeof( IHostedService ), typeof( HostedServiceLifetimeTrigger ), ServiceLifetime.Singleton ) );
            //

            //  - Then an instance of the special "super singleton" EndpointTypeManager is created.
            //    It is the exact same instance that will be available from all the containers: the global and every endpoint containers, it is
            //    the global hook, the relay to the global service provider for the endpoint containers.
            var theEPTM = new EndpointTypeManager_CK();
            var commonDescriptors = theEPTM.CreateCommonDescriptors( this );
            reg.Services.AddRange( commonDescriptors );

            // ServiceDescriptors are created from the EngineStObjMap and added to the global configuration
            // and to the mappings.
            EndpointHelper.FillStObjMappingsWithEndpoints( reg.Monitor, this, reg.Services, mappings );
            // Our StObjMap is empty, but it should have at least the EndpointUbiquitousInfo => EndpointUbiquitousInfo_CK
            // since EndpointUbiquitousInfo is a IScopedAutoService that uses code generation.
            // So, this is what FillStObjMappingsWithEndpoints would do:
            reg.Services.AddScoped<EndpointUbiquitousInfo, EndpointUbiquitousInfo_CK>();

            // We can now close the global container. Waiting for .Net 8.
            // (reg.Services as Microsoft.Extensions.DependencyInjection.ServiceCollection)?.MakeReadOnly();
            bool success = true;
            //  - Then all the endpointType instances create their own ServiceCollection by processing the 
            //    descriptors from the mappings and adding their own EndpointScopeData<TScopeData> scoped data holder
            //    and the true instance singletons IStObjMap, EndpointTypeManager, the EndpointType and the IEnumerable<IEndpointType>. 
            foreach( IEndpointTypeInternal e in theEPTM._endpointTypes )
            {
                if( !e.ConfigureServices( reg.Monitor, this, mappings, commonDescriptors ) ) success = false;
            }
            return success;
        }

        // This is a code generated method that calls all static RegisterStartupServices methods and then
        // all static ConfigureServices methods (following the topological dependency order).
        void RealObjectConfigureServices( in StObjContextRoot.ServiceRegister register )
        {
        }


    }
}
