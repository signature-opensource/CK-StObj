using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.StObj.Model
{
    /// <summary>
    /// Allows <see cref="IMarshallableAutoService"/> implementation to be marshalled by value
    /// thanks to any serialization/deserialization mechanism.
    /// Such marshaller implementations must be registered and available in the DI container as soon
    /// as the marshallable service is used from a remote/background context.
    /// Whenever a marshaller interface is an Auto service, its registartion automatically declares the <typeparamref name="T"/>
    /// to be a Marshallable service (even if T is not marked with any interface).
    /// </summary>
    /// <typeparam name="T">Type of the service to marshall.</typeparam>
    public interface IMarshaller<T>
    {
        /// <summary>
        /// Writes any information to the binary writer that <see cref="Read(ICKBinaryReader)"/> will use to
        /// instanciate a copy of the <paramref name="service"/>.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="service">The service to marshall.</param>
        void Write( ICKBinaryWriter writer, T service );

        /// <summary>
        /// Reads previously written data and recreate a service instance.
        /// </summary>
        /// <param name="reader">The binary reader to use.</param>
        /// <returns>The marshalled service.</returns>
        T Read( ICKBinaryReader reader );
    }
}
