using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Linq;

using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Service
{
    [TestFixture]
    public class ServiceSimpleMappingTests 
    {
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


        [Test]
        public void service_interfaces_requires_unification_otherwise_ISBase_would_be_ambiguous()
        {
            {
                var collector = TestHelper.CreateStObjCollector();
                collector.RegisterType( typeof( ServiceS1Impl ) );
                collector.RegisterType( typeof( ServiceS2Impl ) );
                var r = TestHelper.GetFailedResult( collector );
                r.CKTypeResult.AutoServices.RootClasses.Should().HaveCount( 2 );
            }
            // Same tests as above but excluding ISBase type: success since
            // ISBase is no more considered a IScopedAutoService.
            {
                var collector = TestHelper.CreateStObjCollector( t => t != typeof( ISBase ) );
                collector.RegisterType( typeof( ServiceS1Impl ) );
                collector.RegisterType( typeof( ServiceS2Impl ) );
                var r = TestHelper.GetSuccessfulResult( collector );
                r.Services.SimpleMappings.ContainsKey( typeof( ISBase ) ).Should().BeFalse();
                r.Services.SimpleMappings[typeof( IS1 )].ClassType.Should().BeSameAs( typeof( ServiceS1Impl ) );
                r.Services.SimpleMappings[typeof( IS2 )].ClassType.Should().BeSameAs( typeof( ServiceS2Impl ) );
                r.Services.SimpleMappings[typeof( ServiceS1Impl )].ClassType.Should().BeSameAs( typeof( ServiceS1Impl ) );
                r.Services.SimpleMappings[typeof( ServiceS2Impl )].ClassType.Should().BeSameAs( typeof( ServiceS2Impl ) );
            }
        }

        [Test]
        public void service_interfaces_with_single_implementation()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( ServiceS1S2Impl ) );
            var r = TestHelper.GetSuccessfulResult( collector );
            r.Services.SimpleMappings[typeof( ISBase )].ClassType.Should().BeSameAs( typeof( ServiceS1S2Impl ) );
            r.Services.SimpleMappings[typeof( IS2 )].ClassType.Should().BeSameAs( typeof( ServiceS1S2Impl ) );
            r.Services.SimpleMappings[typeof( IS1 )].ClassType.Should().BeSameAs( typeof( ServiceS1S2Impl ) );
            r.Services.SimpleMappings[typeof( ServiceS1S2Impl )].ClassType.Should().BeSameAs( typeof( ServiceS1S2Impl ) );
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
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( ServiceUnifiedImpl ) );
            var r = TestHelper.GetSuccessfulResult( collector );
            var interfaces = r.CKTypeResult.AutoServices.LeafInterfaces;
            interfaces.Should().HaveCount( 1 );
            var iSU = interfaces[0];
            iSU.Type.Should().Be( typeof( ISU ) );
            iSU.Interfaces.Select( i => i.Type ).Should().BeEquivalentTo( typeof(ISBase), typeof(IS1), typeof(IS2) );
            r.CKTypeResult.AutoServices.RootClasses.Should().ContainSingle().And.Contain( c => c.Type == typeof( ServiceUnifiedImpl ) );
            r.Services.SimpleMappings[typeof( ISU )].ClassType.Should().BeSameAs( typeof( ServiceUnifiedImpl ) );
            r.Services.SimpleMappings[typeof( IS1 )].ClassType.Should().BeSameAs( typeof( ServiceUnifiedImpl ) );
            r.Services.SimpleMappings[typeof( IS2 )].ClassType.Should().BeSameAs( typeof( ServiceUnifiedImpl ) );
            r.Services.SimpleMappings[typeof( ISBase )].ClassType.Should().BeSameAs( typeof( ServiceUnifiedImpl ) );
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

            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( ServiceImpl1 ) );
            collector.RegisterType( typeof( ServiceImpl3 ) );
            if( solved ) collector.RegisterType( typeof( ResolveByClassUnification ) );

            if( solved )
            {
                var r = TestHelper.GetSuccessfulResult( collector );
                r.Services.SimpleMappings[typeof( IMultiImplService )].ClassType.Should().BeSameAs( typeof( ResolveByClassUnification ) );
                r.Services.SimpleMappings[typeof( ServiceImplBaseBase )].ClassType.Should().BeSameAs( typeof( ResolveByClassUnification ) );
                r.Services.SimpleMappings[typeof( ServiceImplRootProblem )].ClassType.Should().BeSameAs( typeof( ResolveByClassUnification ) );
                r.Services.SimpleMappings[typeof( ServiceImpl2 )].ClassType.Should().BeSameAs( typeof( ResolveByClassUnification ) );
                r.Services.SimpleMappings[typeof( ServiceImpl3 )].ClassType.Should().BeSameAs( typeof( ResolveByClassUnification ) );
                r.Services.SimpleMappings[typeof( ResolveByClassUnification )].ClassType.Should().BeSameAs( typeof( ResolveByClassUnification ) );
                r.Services.SimpleMappings[typeof( ServiceImpl1 )].ClassType.Should().BeSameAs( typeof( ServiceImpl1 ) );
            }
            else
            {
                var r = TestHelper.GetFailedResult( collector );
                var interfaces = r.CKTypeResult.AutoServices.LeafInterfaces;
                interfaces.Should().HaveCount( 1 );
                var classes = r.CKTypeResult.AutoServices.RootClasses;
                classes.Select( c => c.Type ).Should().BeEquivalentTo( typeof( ServiceImplBaseBase ) );
                r.CKTypeResult.AutoServices.ClassAmbiguities.Should().HaveCount( 1 );
                r.CKTypeResult.AutoServices.ClassAmbiguities[0]
                    .Select( c => c.Type )
                    .Should().BeEquivalentTo( typeof( ServiceImplRootProblem ), typeof( ServiceImpl1 ), typeof( ServiceImpl3 ) );
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
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( S1 ) );
            collector.RegisterType( typeof( S2 ) );
            collector.RegisterType( typeof( S3 ) );
            collector.RegisterType( typeof( S4 ) );
            var r = TestHelper.GetSuccessfulResult( collector );
            r.Services.SimpleMappings[typeof( ISBase )].ClassType.Should().BeSameAs( typeof( S1 ) );
            r.Services.SimpleMappings[typeof( S1 )].ClassType.Should().BeSameAs( typeof( S1 ) );
            r.Services.SimpleMappings[typeof( S2 )].ClassType.Should().BeSameAs( typeof( S2 ) );
            r.Services.SimpleMappings[typeof( S3 )].ClassType.Should().BeSameAs( typeof( S3 ) );
            r.Services.SimpleMappings[typeof( S4 )].ClassType.Should().BeSameAs( typeof( S4 ) );
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
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( AbstractS1 ) );
            collector.RegisterType( typeof( AbstractS2 ) );
            collector.RegisterType( typeof( AbstractS3 ) );
            var (r, map) = TestHelper.CompileStObjMap( collector );

            var final = r.Services.SimpleMappings[typeof( ISBase )];
            final.ClassType.Should().NotBeSameAs( typeof( AbstractS1 ) );
            final.ClassType.Should().BeAssignableTo( typeof( AbstractS1 ) );
            r.Services.SimpleMappings[typeof( AbstractS1 )].Should().BeSameAs( final );

            r.Services.SimpleMappings[typeof( AbstractS2 )].ClassType.Should().NotBeSameAs( typeof( AbstractS2 ) );
            r.Services.SimpleMappings[typeof( AbstractS2 )].ClassType.Should().BeAssignableTo( typeof( AbstractS2 ) );
            r.Services.SimpleMappings[typeof( AbstractS3 )].ClassType.Should().NotBeSameAs( typeof( AbstractS3 ) );
            r.Services.SimpleMappings[typeof( AbstractS3 )].ClassType.Should().BeAssignableTo( typeof( AbstractS3 ) );

            var services = new ServiceCollection();
            new StObjContextRoot.ServiceRegister( TestHelper.Monitor, services ).AddStObjMap( map );
            IServiceProvider p = services.BuildServiceProvider();
            var oG = p.GetService( typeof( ISBase ) );
            oG.GetType().FullName.Should().StartWith( "CK._g.AbstractS1" );

        }


    }
}
