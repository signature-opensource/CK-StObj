using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Poco
{
    [TestFixture]
    public class RecordTests
    {
        public interface IWithRecordStruct : IPoco
        {
            public record struct ThingDetail( int Power, List<int> Values, string Name = "Albert" );

            ref ThingDetail? Thing1 { get; }
            ref ThingDetail Thing2 { get; }
        }

        [Test]
        public void record_is_not_yet_supported()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IWithRecordStruct ) );
            TestHelper.GetFailedResult( c );
        }

        // To be investigated...
        public interface IWithGenricRecordStruct : IPoco
        {
            public record struct ThingDetail<T>( int Power, T X, List<int> Values, string Name = "Albert" );

            ref ThingDetail<int>? Thing1 { get; }
            ref ThingDetail<string> Thing2 { get; }
        }


    }
}
