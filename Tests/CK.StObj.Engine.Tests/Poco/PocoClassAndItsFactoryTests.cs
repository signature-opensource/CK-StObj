using CK.CodeGen;
using CK.Core;
using CK.Setup;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using static CK.Testing.StObjEngineTestHelper;
using FluentAssertions;

namespace CK.StObj.Engine.Tests.Poco
{
    [TestFixture]
    public class PocoClassAndItsFactoryTests
    {

        public interface IPocoKnowsItsFactory : IPoco
        {
            int One { get; set; }
        }

        [Test]
        public void poco_knows_its_Factory()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IPocoKnowsItsFactory ) );
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var f = s.GetRequiredService<IPocoFactory<IPocoKnowsItsFactory>>();
            var o = f.Create();
            var f2 = ((IPocoClass)o).Factory;
            f.Should().BeSameAs( f2 );
        }


        // Idea for ReadOnly Poco: introducing the IPocoWriter
        // and an extension of the factory to create a builder instance.
        // With this, read only properties and read only collections 
        // can be safely managed.
        //

        /// <summary>
        /// This interface enables to implement dedicated builders.
        /// Any property with the same name as the readonly ones will be automatically implemented
        /// as a mutable version of its IPoco.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public interface IPocoWriter<out T> where T : IPoco
        {
            /// <summary>
            /// Retrieves the currently edited instance.
            /// A new instance is automatically available to be configured.
            /// </summary>
            /// <returns>The current instance value.</returns>
            T Create();
        }

        /// <summary>
        /// Extends the <see cref="IPocoFactory{T}"/> to support a companion <see cref="IPocoWriter{T}"/>.
        /// </summary>
        /// <typeparam name="T">The Poco type.</typeparam>
        /// <typeparam name="TWriter">The Poco writer type.</typeparam>
        public interface IPocoFactory<out T, out TWriter> : IPocoFactory<T>
            where T : IPoco
            where TWriter : IPocoWriter<T>
        {
            /// <summary>
            /// Creates a writer that can be used to configure a read only Poco.
            /// </summary>
            /// <returns>A writer.</returns>
            TWriter CreateWriter();
        }

        public interface IThing : IPoco
        {
            int One { get; }
        }

        public interface ISuperThing : IThing
        {
            int Two { get; }

            IReadOnlyList<int> List { get; }
        }

        public interface IOtherThing : IThing
        {
            string SomethingElse { get; }
        }

        public interface IThingWriter : IPocoWriter<IThing>
        {
            int One { get; set; }
        }

        public interface ISuperThingWriter : IThingWriter
        {
            int Two { get; set; }

            IList<int> List { get; }
        }

        public interface IOtherThingWriter : IThingWriter
        {
            string SetSomethingElse { get; set; }
        }


    }
}
