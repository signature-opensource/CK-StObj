using CK.Core;
using CK.Setup;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Reflection;
using static CK.Testing.MonitorTestHelper;

namespace CK.Engine.TypeCollector.Tests;

[TestFixture]
public class AssemblyAndTypeCacheTests
{
    [Test]
    public void TypeCache_on_generics()
    {
        var config = new EngineConfiguration();
        config.FirstBinPath.Path = TestHelper.BinFolder;
        config.FirstBinPath.Assemblies.Add( "CK.Engine.TypeCollector.Tests" );

        var typeCache = AssemblyCache.Run( TestHelper.Monitor, config ).TypeCache;

        var v8 = typeCache.Get( typeof( ValueTuple<,,,,,,,> ) );
        v8.CSharpName.Should().Be( "System.ValueTuple<T1,T2,T3,T4,T5,T6,T7,TRest>" );
        var gParameters = v8.GenericParameters;
        gParameters.Select( p => p.Name ).Concatenate().Should().Be( "T1, T2, T3, T4, T5, T6, T7, TRest" );
        v8.GenericArguments.Should().BeEmpty();

        var oneV8 = (1, "string", 3, (object?)null, 5, typeof( void ), 7, "8");
        var iV8 = typeCache.Get( oneV8.GetType() );
        iV8.GenericParameters.Should().BeEmpty();
        var gArguments = iV8.GenericArguments;
        gArguments.Select( p => p.ToString() ).Concatenate().Should().Be( "int, string, int, object, int, System.Type, int, (string)" );
    }

    /// <summary>
    /// This browses a big set of types from mscorlib, CK.Core.ActivityMonitor and CK.Core.ActivityMonitor.
    /// Among them (especially mscorlib), all kind of types can be found.
    /// We use the ToString() method of the DeclaredMethodInfo to walk across the types and check that
    /// the CK.Core.TypeExtensions.ToCSharpName can also handle everything.
    /// </summary>
    [Test]
    public void TypeCache_can_handle_all_types()
    {
        var config = new EngineConfiguration();
        config.FirstBinPath.Path = TestHelper.BinFolder;
        config.FirstBinPath.Assemblies.Add( "CK.Engine.TypeCollector.Tests" );

        var typeCache = AssemblyCache.Run( TestHelper.Monitor, config ).TypeCache;

        DumpTypes( TestHelper.Monitor, typeCache, typeof( CK.Core.ActivityMonitorSimpleSenderExtension ).Assembly );
        DumpTypes( TestHelper.Monitor, typeCache, typeof( CK.Core.ActivityMonitor ).Assembly );
        DumpTypes( TestHelper.Monitor, typeCache, typeof( string ).Assembly );

        static void DumpTypes( IActivityMonitor monitor, GlobalTypeCache typeCache, Assembly a )
        {
            using( monitor.OpenInfo( a.ToString() ) )
            {
                foreach( var t in a.GetTypes() )
                {
                    var cT = typeCache.Get( t );
                    using( monitor.OpenInfo( cT.ToString() ) )
                    {
                        cT.Type.Should().Be( t );
                        foreach( var m in cT.DeclaredMethodInfos )
                        {
                            m.ToString().Should().NotBeNull();
                        }
                        cT.GenericArguments.IsDefault.Should().BeFalse();
                        cT.GenericParameters.IsDefault.Should().BeFalse();
                    }
                }
            }
        }
    }

    // See https://stackoverflow.com/a/6704993/190380
    class Outer<TOuter>
    {
        public class XClass<T, U> { }
    }

    class SemiOpen<T> : Outer<T>.XClass<T,int> { }

    [Test]
    public void TypeCache_on_semi_open()
    {
        var config = new EngineConfiguration();
        config.FirstBinPath.Path = TestHelper.BinFolder;
        config.FirstBinPath.Assemblies.Add( "CK.Engine.TypeCollector.Tests" );
        var typeCache = AssemblyCache.Run( TestHelper.Monitor, config ).TypeCache;

        {
            Type t = typeof( SemiOpen<> );
            Type? tBase = t.BaseType;
            Throw.DebugAssert( tBase != null );

            var cT = typeCache.Get( tBase );
            cT.IsTypeDefinition.Should().BeTrue();
            cT.IsGenericType.Should().BeFalse();
            cT.GenericTypeDefinition.Should().BeNull();
            cT.ToString().Should().Be( "CK.Engine.TypeCollector.Tests.AssemblyAndTypeCacheTests.SomeGen<T>.Sub<TOther>" );
            cT.GenericParameters.Should().HaveCount( 2 );
            cT.GenericArguments.Should().BeEmpty();
        }
    }

}
