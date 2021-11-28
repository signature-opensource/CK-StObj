using CK.Core;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Service
{
    /// <summary>
    /// The idea of this "ServiceDispatcher" is that a class that implements an auto service interface
    /// and has a ctor parameter that is a IEnumerable of such service is an unifier.
    /// A better name could be: "UnifierService" since one could say that "Service Unification" relies on
    /// interface (more precisely on enumerable of service interfaces) and "Class Unification" relies on classes.
    /// </summary>
    [TestFixture]
    public class DispatcherServiceTests
    {
        public interface IServiceBase : IScopedAutoService
        {
            int CountOfThings { get; }
        }

        public class S1 : IServiceBase
        {
            public int CountOfThings => 1;
        }

        public class S2 : IServiceBase
        {
            public int CountOfThings => 2;
        }

        public class SDispatcher : IServiceBase
        {
            readonly IEnumerable<IServiceBase> _others;

            public SDispatcher( IEnumerable<IServiceBase> others )
            {
                _others = others;
            }

            public int CountOfThings => _others.Select( o => o.CountOfThings ).Sum();

        }

        [Test]
        public void simple_dispatcher_on_IEnumerable_of_IAutoService_is_not_yet_supported()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( S1 ) );
            collector.RegisterType( typeof( S2 ) );
            collector.RegisterType( typeof( SDispatcher ) );
            TestHelper.GetFailedResult( collector );
        }


    }
}
