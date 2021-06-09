using CK.CodeGen;
using CK.Core;
using CK.Setup;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.Json;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.PocoJson
{
    [TestFixture]
    public partial class NumberAndStringTests
    {
        [ExternalName( "Numerics" )]
        public interface INumerics : IPoco
        {
            byte Byte { get; set; }
            sbyte SByte { get; set; }
            short Short { get; set; }
            ushort UShort { get; set; }
            int Integer { get; set; }
            uint UInteger { get; set; }
            long Long { get; set; }
            ulong ULong { get; set; }
            float Float { get; set; }
            double Double { get; set; }
        }

        [Test]
        public void all_numerics_successfully_roundtrip()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( INumerics ) ); ;
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var directory = s.GetService<PocoDirectory>();

            var nMax = s.GetService<IPocoFactory<INumerics>>().Create();
            nMax.Byte = Byte.MaxValue;
            nMax.SByte = SByte.MaxValue;
            nMax.Short = Int16.MaxValue;
            nMax.UShort = UInt16.MaxValue;
            nMax.Integer = Int32.MaxValue;
            nMax.UInteger = UInt32.MaxValue;
            nMax.Long = Int64.MaxValue;
            nMax.ULong = UInt64.MaxValue;
            nMax.Float = Single.MaxValue;
            nMax.Double = Double.MaxValue;

            var nMin = s.GetService<IPocoFactory<INumerics>>().Create();
            nMin.Byte = Byte.MinValue;
            nMin.SByte = SByte.MinValue;
            nMin.Short = Int16.MinValue;
            nMin.UShort = UInt16.MinValue;
            nMin.Integer = Int32.MinValue;
            nMin.UInteger = UInt32.MinValue;
            nMin.Long = Int64.MinValue;
            nMin.ULong = UInt64.MinValue;
            nMin.Float = Single.MinValue;
            nMin.Double = Double.MinValue;

            var nMax2 = JsonTestHelper.Roundtrip( directory, nMax, text => TestHelper.Monitor.Info( $"INumerics(max) serialization: " + text ) );
            nMax2.Should().BeEquivalentTo( nMax );

            var nMin2 = JsonTestHelper.Roundtrip( directory, nMin, text => TestHelper.Monitor.Info( $"INumerics(min) serialization: " + text ) );
            nMin2.Should().BeEquivalentTo( nMin );
        }

    }
}
