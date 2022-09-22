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
    public class PocoGenericTests
    {
        public interface IAmAmbiguous<T> : IPoco
        {
            T Value { get; set; }
        }

        public interface IWantAnInt : IAmAmbiguous<int>
        {
        }

        public interface IWantAnObject : IAmAmbiguous<object>
        {
        }


        [Test]
        public void generic_IPoco_is_forbidden()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IWantAnInt ) );
            TestHelper.GetFailedResult( c );
        }

        [CKTypeDefiner]
        public interface IGenericDefiner<T> : IPoco where T : class, IGenericItem
        {
            /// <summary>
            /// Gets a mutable list of <see cref="IMissionLine"/>.
            /// </summary>
            List<T> Items { get; }
        }

        [CKTypeDefiner]
        public interface IGenericItem : IPoco
        {
        }

        [ExternalName("ConcretePoco")]
        public interface IConcretePoco : IGenericDefiner<IConcreteItem>
        {
        }

        public interface IConcreteItem : IGenericItem
        {
        }

        [Test]
        public void generic_IPoco_definer_is_possible()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IConcretePoco ), typeof( IConcreteItem ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var d = s.GetRequiredService<PocoDirectory>();
            var root = d.Create<IConcretePoco>();
            var item = d.Create<IConcreteItem>();
            root.Items.Add( item );
        }

    }
}
