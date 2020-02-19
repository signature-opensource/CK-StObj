using System.Reflection;
using CK.Core;

using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.SimpleObjects
{
    public static class SimpleObjectsTrace
    {
        public static void LogMethod( MethodBase m )
        {
            TestHelper.Monitor.Trace( $"{m.DeclaringType.Name}.{m.Name} {(m.IsVirtual ? "(virtual)" : "")} has been called." );
        }
    }
}
