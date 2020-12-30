using System.Diagnostics;
using System.Reflection;
using CK.Core;

using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.SimpleObjects
{
    public static class SimpleObjectsTrace
    {
        public static void LogMethod( MethodBase? m )
        {
            Debug.Assert( m != null && m.DeclaringType != null, "There is no reason for method to be null." );
            TestHelper.Monitor.Trace( $"{m.DeclaringType.Name}.{m.Name} {(m.IsVirtual ? "(virtual)" : "")} has been called." );
        }
    }
}
