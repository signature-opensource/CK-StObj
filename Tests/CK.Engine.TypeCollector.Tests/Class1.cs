using CK.Core;
using CK.Setup;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Reflection;
using static CK.Testing.MonitorTestHelper;

namespace CK.Engine.TypeCollector.Tests;

[TestFixture]
public class AssemblyAndTypeCacheTests
{
    [Test]
    public void TypeCache_can_handle_all_types()
    {
        var config = new EngineConfiguration();
        config.FirstBinPath.Path = TestHelper.BinFolder;
        config.FirstBinPath.Assemblies.Add( "CK.Engine.TypeCollector.Tests" );

        var typeCache = AssemblyCache.Run( TestHelper.Monitor, config ).TypeCache;

        typeCache.Get( typeof(ValueTuple<,,,,,,,>) ).CSharpName.Should().Be( "System.ValueTuple<T1,T2,T3,T4,T5,T6,T7,TRest>" );

        DumpTypes( TestHelper.Monitor, typeCache, typeof( CK.Core.ActivityMonitorSimpleSenderExtension ).Assembly );
        DumpTypes( TestHelper.Monitor, typeCache, typeof( CK.Core.ActivityMonitor ).Assembly );
        DumpTypes( TestHelper.Monitor, typeCache, typeof( string ).Assembly );

        static void DumpTypes( IActivityMonitor monitor, GlobalTypeCache typeCache, Assembly a )
        {
            using( monitor.OpenInfo( a.ToString() ) )
            {
                foreach( var t in a.GetTypes() )
                {
                    monitor.Debug( $"Type: {t.AssemblyQualifiedName}" );
                    var cT = typeCache.Get( t );
                    using( monitor.OpenInfo( cT.ToString() ) )
                    {
                        cT.Type.Should().Be( t );
                        foreach( var m in cT.DeclaredMethodInfos )
                        {
                            monitor.Debug( $"Method: {m.ToString()}" );
                            TestHelper.Monitor.Info( m.ToString() );
                        }
                    }
                }
            }
        }

    }
}
