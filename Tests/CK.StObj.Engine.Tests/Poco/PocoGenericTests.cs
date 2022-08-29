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

    }
}
