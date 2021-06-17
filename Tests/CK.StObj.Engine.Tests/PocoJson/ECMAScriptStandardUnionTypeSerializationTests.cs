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
    public partial class ECMAScriptStandardUnionTypeSerializationTests
    {
        public static readonly PocoJsonSerializerOptions Standard = new PocoJsonSerializerOptions { Mode = PocoJsonSerializerMode.ECMAScriptStandard };

        public interface IOther : IPoco { public int Value { get; set; } }

        [ExternalName("UT")]
        public interface INotCompliant1 : IPoco
        {
            [UnionType]
            public object Thing { get; set; }

            class UnionTypes
            {
                public (double, int) Thing { get; }
            }
        }

        [ExternalName("UT")]
        public interface INotCompliant2 : IPoco
        {
            [UnionType]
            public object Thing { get; set; }

            class UnionTypes
            {
                public (List<int>, int[]) Thing { get; }
            }
        }

        [ExternalName("UT")]
        public interface INotCompliant3 : IPoco
        {
            [UnionType]
            public object Thing { get; set; }

            class UnionTypes
            {
                public (List<int>, int?[]) Thing { get; }
            }
        }

        [ExternalName("UT")]
        public interface INotCompliant4 : IPoco
        {
            [UnionType]
            public object Thing { get; set; }

            class UnionTypes
            {
                public (List<(int,string)>, (int,string)?[]) Thing { get; }
            }
        }

        [Test]
        public void Non_compliant_Poco_are_detected()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( INotCompliant1 ) );
            var services = TestHelper.GetAutomaticServices( c ).Services;
            var directory = services.GetService<PocoDirectory>();

            var u = services.GetService<IPocoFactory<INotCompliant1>>().Create();

            FluentActions.Invoking( () => u.JsonSerialize( true, Standard ) ).Should().Throw<NotSupportedException>();
            FluentActions.Invoking( () => u.JsonSerialize( false, Standard ) ).Should().Throw<NotSupportedException>();
            FluentActions.Invoking( () => directory.JsonDeserialize( @"[""UT"",{""Thing"":3}]", Standard ) ).Should().Throw<NotSupportedException>();
        }

    }
}
