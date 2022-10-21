using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Poco
{
    [TestFixture]
    public class AutoInstantiatedPropertyTests
    {
        [CKTypeDefiner]
        public interface IHaveAutoProperty : IPoco
        {
            object Auto { get; }
        }

        #region Reference type (List)
        public interface IAutoListPrimary1 : IPoco, IHaveAutoProperty
        {
            new List<string> Auto { get; }
        }
        public interface IAutoListExtension1 : IAutoListPrimary1
        {
            new List<string>? Auto { get; }
        }

        public interface IAutoListPrimary2 : IPoco, IHaveAutoProperty
        {
            new List<string>? Auto { get; }
        }
        public interface IAutoListExtension2 : IAutoListPrimary2
        {
            new List<string> Auto { get; }
        }
        #endregion

        #region Value type (int)
        public interface IAutoIntPrimary1 : IPoco, IHaveAutoProperty
        {
            new int Auto { get; }
        }
        public interface IAutoIntExtension1 : IAutoIntPrimary1
        {
            new int? Auto { get; }
        }

        public interface IAutoIntPrimary2 : IPoco, IHaveAutoProperty
        {
            new int? Auto { get; }
        }
        public interface IAutoIntExtension2 : IAutoIntPrimary2
        {
            new int Auto { get; }
        }
        #endregion

        #region Value type (anonymous record)
        public interface IAutoAnonymousRecordPrimary1 : IPoco, IHaveAutoProperty
        {
            new ref (int A, string B) Auto { get; }
        }
        public interface IAutoAnonymousRecordExtension1 : IAutoAnonymousRecordPrimary1
        {
            new (int A, string B)? Auto { get; }
        }

        public interface IAutoAnonymousRecordPrimary2 : IPoco, IHaveAutoProperty
        {
            new (int A, string B)? Auto { get; }
        }
        public interface IAutoAnonymousRecordExtension2 : IAutoAnonymousRecordPrimary2
        {
            new ref (int A, string B) Auto { get; }
        }
        #endregion

        [TestCase( typeof( List<string> ), typeof( IAutoListPrimary1 ), typeof( IAutoListExtension1 ) )]
        [TestCase( typeof( List<string> ), typeof( IAutoListPrimary2 ), typeof( IAutoListExtension2 ) )]
        [TestCase( typeof( int ), typeof( IAutoIntPrimary1 ), typeof( IAutoIntExtension1 ) )]
        [TestCase( typeof( int ), typeof( IAutoIntPrimary2 ), typeof( IAutoIntExtension2 ) )]
        [TestCase( typeof( (int,string) ), typeof( IAutoAnonymousRecordPrimary1 ), typeof( IAutoAnonymousRecordExtension1 ) )]
        [TestCase( typeof( (int, string) ), typeof( IAutoAnonymousRecordPrimary2 ), typeof( IAutoAnonymousRecordExtension2 ) )]
        public void auto_initialized_property_can_be_exposed_as_nullable_properties( Type tAutoProperty, Type tPrimary, Type tExtension )
        {
            var c = TestHelper.CreateStObjCollector( tPrimary, tExtension );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var d = s.GetRequiredService<PocoDirectory>();
            var f = d.Find( tPrimary );
            Debug.Assert( f != null );
            f.Should().BeSameAs( d.Find( tExtension ) );
            var o = (IHaveAutoProperty)f.Create();
            o.Auto.Should().NotBeNull().And.BeOfType( tAutoProperty );
        }

    }
}
