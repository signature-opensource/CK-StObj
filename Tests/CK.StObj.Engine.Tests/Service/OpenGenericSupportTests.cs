using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Service
{
    public class OpenGenericSupportTests
    {

        [CKTypeSuperDefiner]
        public interface IUsefulService<out T> : IAutoService
        {
            T Value { get; }
        }

        public interface IMyServiceTemplate<T> : IUsefulService<T>
        {
            void SetValue( T v );
        }

        public class ClassService : IMyServiceTemplate<int>
        {
            public int Value => 5;

            public void SetValue( int v ) { }
        }


        [Test]
        public void super_definer_applies_to_final_class()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( ClassService ) );
            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            map.Services.SimpleMappings.ContainsKey( typeof( IUsefulService<int> ) ).Should().BeFalse( "The SuperDefiner." );
            map.Services.SimpleMappings.ContainsKey( typeof( IMyServiceTemplate<int> ) ).Should().BeFalse( "The Definer." );
            map.Services.SimpleMappings[typeof( ClassService )].IsScoped.Should().BeFalse();
        }

        public interface InterfaceService : IMyServiceTemplate<int>
        {
        }


        public class ClassFromInterfaceService : InterfaceService
        {
            public int Value => 5;

            public void SetValue( int v ) { }
        }

        [Test]
        public void super_definer_applies_to_final_interface()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( ClassFromInterfaceService ) );
            var r = TestHelper.CreateAutomaticServices( collector );
            Debug.Assert( r.Result.EngineMap != null, "No initialization error." );

            try
            {
                r.Result.EngineMap.Services.SimpleMappings.ContainsKey( typeof( IUsefulService<int> ) ).Should().BeFalse( "The SuperDefiner." );
                r.Result.EngineMap.Services.SimpleMappings.ContainsKey( typeof( IMyServiceTemplate<int> ) ).Should().BeFalse( "The Definer." );

                r.Result.EngineMap.Services.SimpleMappings.ContainsKey( typeof( InterfaceService ) ).Should().BeTrue();
                r.Result.EngineMap.Services.SimpleMappings[typeof( ClassFromInterfaceService )].UniqueMappings.Should().BeEquivalentTo(
                    new[] { typeof( InterfaceService ) } );

                r.Services.GetService<InterfaceService>().Should().Be( r.Services.GetService<ClassFromInterfaceService>() );
            }
            finally
            {
                r.Services.Dispose();
            }
        }

    }
}
