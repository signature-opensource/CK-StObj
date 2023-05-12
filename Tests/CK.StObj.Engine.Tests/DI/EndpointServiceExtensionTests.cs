using CK.Core;
using NUnit.Framework;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.DI
{
    [TestFixture]
    public class EndpointServiceExtensionTests
    {
        public class AnotherEndpointType : EndpointType
        {
        }

        [EndpointServiceAvailability( typeof( DefaultEndpointType ) )]
        public interface IEPService1 : IScopedAutoService
        {
        }

        [EndpointServiceAvailability( typeof( AnotherEndpointType ) )]
        public interface IEPService2 : IEPService1
        {
        }

        [Test]
        public void IAutoService_Endpoint_services_cannot_extend_their_endpoints()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IEPService1 ), typeof( IEPService2 ) );
            TestHelper.GetFailedResult( c );
        }
    }
}
