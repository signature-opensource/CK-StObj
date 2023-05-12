using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Linq;

using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Service
{
    [TestFixture]
    public class ServiceSimpleMappingTests 
    {
        // The 2 services below can be singleton: IAutoService would lead to singletons.
        // Specifying scoped is not the default.
        public interface ISBase : IScopedAutoService
        {
        }

        public interface IS1 : ISBase
        {
        }

        public interface IS2 : ISBase
        {
        }

        public class ServiceS1Impl : IS1
        {
        }

        public class ServiceS2Impl : IS2
        {
        }

        public class ServiceS1S2Impl : IS1, IS2
        {
        }


        // This test shows that excluding a base interface does not exclude the interfaces it implies.
        //
        // Type exclusion is currently "weak". A "stronger" exclusion (by analyzing declared interfaces) would de facto
        // completely exclude any service implementations since IAutoService would not appear anymore.
        //
        // For interfaces this makes sense: here the ISBase being excluded, no more ISBase service would be available.
        // It would be up to the developer to reintroduce IS1 and/or IS2 into the CK type system by explicitly configuring them
        // thanks to the "SetAutoServiceKind" API.
        //
        // # Weak exclusion
        //
        // The type itself is ignored but not its impacts. To remove a "family" of services, one need to exclude explicitly
        // all of them.
        // Excluding a [CKTypeDefiner] doesn't "undefine" its specializations. Same as above.
        //
        // Weak exclusion may be renamed into "Type masking" or "Type hiding". This doesn't change the code, the structure of the
        // program, instead it just says "don't use this type".
        //
        // # Strong exclusion
        // 
        // Weak exclusion is definitely not "logically sound" and it is not easy to reason about it. Strong exclusion is easier:
        // the type is cleared from any "CK Type" impact... But is it?
        //
        // "Strongly" excluding a base class for instance means to remove all impacts of the class on its specializations. Among
        // these impacts there is the IRealObject interface, so to reintroduce some specializations of it we would need to expose a
        // "SetRealObjectKind" method and offer external configuration support like the one for AutoService.
        // Tedious but feasible...
        // 
        // Excluding a [CKTypeSuperTypeDefiner] condemns is specializations in the same manner.
        //
        // Propagation through inheritance can be handled (but, still, the type appear in the hierarchy). For attributes, it's
        // more problematic. Should the attributes that drives auto-implementation of abstract methods for instance be skipped?
        // If yes, some abstract methods may never be implemented (final code generation will fail) but since the type is excluded
        // so it must not be used... So one can consider that this is fine.
        //
        // # But why do we need to exclude some types?
        //
        // One reason is to resolve ambiguities between types, basically between services or real objects implementations.
        // Here, leaf types are concerned: weak exclusion is fine.
        //
        // This is actually the only good reason. Using type exclusion as a way to configure a System is a dead end, a maintenance
        // nightmare. I can't think of any other (good) reason to use it.
        //
        // If "type exclusion is only a way to resolve type mapping ambiguities and should never be used as a way to configure or change the code" then:
        // - Using it on base types make very little sense: leaf types are primarily concerned.
        // - Whether exclusion is "weak" or "strong" doesn't really matter: they really differ when a base type is excluded.
        //
        // Current exclusion semantics is what it is, not perfect and used rarely and quite always on leaf types, so let it be.
        [Test]
        public void service_interfaces_requires_unification_otherwise_ISBase_would_be_ambiguous()
        {
            //{
            //    var collector = TestHelper.CreateStObjCollector();
            //    collector.RegisterType( typeof( ServiceS1Impl ) );
            //    collector.RegisterType( typeof( ServiceS2Impl ) );
            //    var r = TestHelper.GetFailedResult( collector );
            //    Debug.Assert( r != null, "We have a (failed) result." );
            //    r.CKTypeResult.AutoServices.RootClasses.Should().HaveCount( 2 );
            //}
            // Same tests as above but excluding ISBase type: success since
            // ISBase is no more considered a IScopedAutoService.
            {
                var collector = TestHelper.CreateStObjCollector( t => t != typeof( ISBase ) );
                collector.RegisterType( TestHelper.Monitor, typeof( ServiceS1Impl ) );
                collector.RegisterType( TestHelper.Monitor, typeof( ServiceS2Impl ) );
                var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
                Debug.Assert( map != null, "No initialization error." );

                map.Services.SimpleMappings.ContainsKey( typeof( ISBase ) ).Should().BeFalse();

                Setup.IStObjServiceFinalSimpleMapping s1 = map.Services.SimpleMappings[typeof( IS1 )];
                s1.ClassType.Should().BeSameAs( typeof( ServiceS1Impl ) );
                s1.IsScoped.Should().BeTrue( "Excluding ISBase keeps IScopedAutoService (and hence the IAutoService) base." );

                map.Services.SimpleMappings[typeof( IS2 )].ClassType.Should().BeSameAs( typeof( ServiceS2Impl ) );
                map.Services.SimpleMappings[typeof( ServiceS1Impl )].ClassType.Should().BeSameAs( typeof( ServiceS1Impl ) );
                map.Services.SimpleMappings[typeof( ServiceS2Impl )].ClassType.Should().BeSameAs( typeof( ServiceS2Impl ) );
            }
        }

        [Test]
        public void service_interfaces_with_single_implementation()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( ServiceS1S2Impl ) );
            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );
            map.Services.SimpleMappings[typeof( ISBase )].ClassType.Should().BeSameAs( typeof( ServiceS1S2Impl ) );
            map.Services.SimpleMappings[typeof( IS2 )].ClassType.Should().BeSameAs( typeof( ServiceS1S2Impl ) );
            map.Services.SimpleMappings[typeof( IS1 )].ClassType.Should().BeSameAs( typeof( ServiceS1S2Impl ) );
            map.Services.SimpleMappings[typeof( ServiceS1S2Impl )].ClassType.Should().BeSameAs( typeof( ServiceS1S2Impl ) );
        }

        public interface ISU : IS1, IS2
        {
        }

        public class ServiceUnifiedImpl : ISU
        {
        }

        [Test]
        public void service_interfaces_unification_works()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( ServiceUnifiedImpl ) );
            var r = TestHelper.GetSuccessfulResult( collector );
            var interfaces = r.CKTypeResult.AutoServices.LeafInterfaces;
            interfaces.Should().HaveCount( 1 );
            var iSU = interfaces[0];
            iSU.Type.Should().Be( typeof( ISU ) );
            iSU.Interfaces.Select( i => i.Type ).Should().BeEquivalentTo( new[] { typeof( ISBase ), typeof( IS1 ), typeof( IS2 ) } );
            r.CKTypeResult.AutoServices.RootClasses.Should().Contain( c => c.ClassType == typeof( ServiceUnifiedImpl ) || c.ClassType == typeof( EndpointTypeManager ) );
            Debug.Assert( r.EngineMap != null, "No initialization error." );
            r.EngineMap.Services.SimpleMappings[typeof( ISU )].ClassType.Should().BeSameAs( typeof( ServiceUnifiedImpl ) );
            r.EngineMap.Services.SimpleMappings[typeof( IS1 )].ClassType.Should().BeSameAs( typeof( ServiceUnifiedImpl ) );
            r.EngineMap.Services.SimpleMappings[typeof( IS2 )].ClassType.Should().BeSameAs( typeof( ServiceUnifiedImpl ) );
            r.EngineMap.Services.SimpleMappings[typeof( ISBase )].ClassType.Should().BeSameAs( typeof( ServiceUnifiedImpl ) );
        }

        public interface IMultiImplService : IScopedAutoService
        {
        }

        // Intermediate class.
        public class ServiceImplBaseBase : IMultiImplService
        {
        }

        // Root class with 2 ambiguous specializations (ServiceImpl1 and ServiceImpl3).
        public class ServiceImplRootProblem : ServiceImplBaseBase
        {
        }

        // First ambiguous class.
        public class ServiceImpl1 : ServiceImplRootProblem
        {
        }

        // Intermediate class.
        public class ServiceImpl2 : ServiceImplRootProblem
        {
        }

        // Second ambiguous class.
        public class ServiceImpl3 : ServiceImpl2
        {
        }

        // Solver (uses Class Unification).
        public class ResolveByClassUnification : ServiceImpl3
        {
            public ResolveByClassUnification( ServiceImpl1 s1 )
            {
            }
        }

        [TestCase( "With Service Chaining" )]
        [TestCase( "Without" )]
        public void service_classes_ambiguities_that_requires_Service_Chaining( string mode )
        {
            bool solved = mode == "With Service Chaining";

            var collector = TestHelper.CreateStObjCollector( typeof( ServiceImpl1 ), typeof( ServiceImpl3 ) );
            if( solved ) collector.RegisterType( TestHelper.Monitor, typeof( ResolveByClassUnification ) );

            if( solved )
            {
                var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
                Debug.Assert( map != null, "No initialization error." );

                map.Services.SimpleMappings[typeof( IMultiImplService )].ClassType.Should().BeSameAs( typeof( ResolveByClassUnification ) );
                map.Services.SimpleMappings[typeof( ServiceImplBaseBase )].ClassType.Should().BeSameAs( typeof( ResolveByClassUnification ) );
                map.Services.SimpleMappings[typeof( ServiceImplRootProblem )].ClassType.Should().BeSameAs( typeof( ResolveByClassUnification ) );
                map.Services.SimpleMappings[typeof( ServiceImpl2 )].ClassType.Should().BeSameAs( typeof( ResolveByClassUnification ) );
                map.Services.SimpleMappings[typeof( ServiceImpl3 )].ClassType.Should().BeSameAs( typeof( ResolveByClassUnification ) );
                map.Services.SimpleMappings[typeof( ResolveByClassUnification )].ClassType.Should().BeSameAs( typeof( ResolveByClassUnification ) );
                map.Services.SimpleMappings[typeof( ServiceImpl1 )].ClassType.Should().BeSameAs( typeof( ServiceImpl1 ) );
            }
            else
            {
                var r = TestHelper.GetFailedResult( collector, "cannot be unified by any of this candidates: " );
                Debug.Assert( r != null, "We have a (failed) result." );

                var interfaces = r.CKTypeResult.AutoServices.LeafInterfaces;
                interfaces.Should().HaveCount( 1 );
                var classes = r.CKTypeResult.AutoServices.RootClasses;
                classes.Select( c => c.ClassType ).Should().BeEquivalentTo( new[] { typeof( ServiceImplBaseBase ), typeof( EndpointTypeManager ) } );
                r.CKTypeResult.AutoServices.ClassAmbiguities.Should().HaveCount( 1 );
                r.CKTypeResult.AutoServices.ClassAmbiguities[0]
                    .Select( c => c.ClassType )
                    .Should().BeEquivalentTo( new[] { typeof( ServiceImplRootProblem ), typeof( ServiceImpl1 ), typeof( ServiceImpl3 ) } );
            }
        }

        public class S1 : ISBase
        {
            public S1( S2 s2 )
            {
            }
        }

        public class S2 : ISBase
        {
            public S2( S3 s2 )
            {
            }
        }

        public class S3 : ISBase
        {
            public S3( S4 s2 )
            {
            }
        }

        public class S4 : ISBase
        {
        }

        [Test]
        public void simple_linked_list_of_service_classes()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( S1 ), typeof( S2 ), typeof( S3 ), typeof( S4 ) );
            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );
            map.Services.SimpleMappings[typeof( ISBase )].ClassType.Should().BeSameAs( typeof( S1 ) );
            map.Services.SimpleMappings[typeof( S1 )].ClassType.Should().BeSameAs( typeof( S1 ) );
            map.Services.SimpleMappings[typeof( S2 )].ClassType.Should().BeSameAs( typeof( S2 ) );
            map.Services.SimpleMappings[typeof( S3 )].ClassType.Should().BeSameAs( typeof( S3 ) );
            map.Services.SimpleMappings[typeof( S4 )].ClassType.Should().BeSameAs( typeof( S4 ) );
        }

        public abstract class AbstractS1 : ISBase
        {
            public AbstractS1( AbstractS2 s2 )
            {
            }
        }

        public abstract class AbstractS2 : ISBase
        {
            public AbstractS2( AbstractS3 s3 )
            {
            }
        }

        public abstract class AbstractS3 : ISBase
        {
            public AbstractS3()
            {
            }
        }

        [Test]
        public void Linked_list_of_service_abstract_classes()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( AbstractS1 ), typeof( AbstractS2 ), typeof( AbstractS3 ) );

            var (r, map) = TestHelper.CompileAndLoadStObjMap( collector );

            Debug.Assert( r.EngineMap != null, "No initialization error." );
            var final = r.EngineMap.Services.SimpleMappings[typeof( ISBase )];
            final.FinalType.Should().NotBeSameAs( typeof( AbstractS1 ) );
            final.FinalType.Should().BeAssignableTo( typeof( AbstractS1 ) );
            r.EngineMap.Services.SimpleMappings[typeof( AbstractS1 )].Should().BeSameAs( final );

            r.EngineMap.Services.SimpleMappings[typeof( AbstractS2 )].FinalType.Should().NotBeSameAs( typeof( AbstractS2 ) );
            r.EngineMap.Services.SimpleMappings[typeof( AbstractS2 )].FinalType.Should().BeAssignableTo( typeof( AbstractS2 ) );
            r.EngineMap.Services.SimpleMappings[typeof( AbstractS3 )].FinalType.Should().NotBeSameAs( typeof( AbstractS3 ) );
            r.EngineMap.Services.SimpleMappings[typeof( AbstractS3 )].FinalType.Should().BeAssignableTo( typeof( AbstractS3 ) );

            var services = new ServiceCollection();
            new StObjContextRoot.ServiceRegister( TestHelper.Monitor, services ).AddStObjMap( map ).Should().BeTrue( "ServiceRegister.AddStObjMap doesn't throw." );
            IServiceProvider p = services.BuildServiceProvider();

            var oG = p.GetRequiredService( typeof( ISBase ) );
            oG.GetType().FullName.Should().Be( "CK.StObj.Engine.Tests.Service.ServiceSimpleMappingTests_AbstractS1_CK" );
        }


    }
}
