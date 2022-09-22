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
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var f = s.GetRequiredService<IPocoFactory<IPocoKnowsItsFactory>>();
            var o = f.Create();
            var f2 = ((IPocoGeneratedClass)o).Factory;
            f.Should().BeSameAs( f2 );
        }


        // Idea for ReadOnly Poco: introducing the IPocoBuilder
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
        public interface IPocoBuilder<out T> where T : IPoco
        {
            /// <summary>
            /// Retrieves the currently edited instance.
            /// A new instance is automatically available to be configured.
            /// </summary>
            /// <returns>The current instance value.</returns>
            T Create();
        }

        /// <summary>
        /// Extends the <see cref="IPocoFactory{T}"/> to support a companion <see cref="IPocoBuilder{T}"/>.
        /// </summary>
        /// <typeparam name="T">The Poco type.</typeparam>
        /// <typeparam name="TBuilder">The Poco builder type.</typeparam>
        public interface IPocoFactory<out T, out TBuilder> : IPocoFactory<T>
            where T : IPoco
            where TBuilder : IPocoBuilder<T>
        {
            /// <summary>
            /// Creates a writer that can be used to configure a read only Poco.
            /// </summary>
            /// <returns>A builder.</returns>
            TBuilder CreateBuilder();
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

        public interface IThingBuilder : IPocoBuilder<IThing>
        {
            int One { get; set; }
        }

        public interface ISuperThingBuilder : IThingBuilder
        {
            int Two { get; set; }

            List<int> List { get; }
        }

        public interface IOtherThingBuilder : IThingBuilder
        {
            string SetSomethingElse { get; set; }
        }

    }
}
