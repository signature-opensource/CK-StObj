using CK.Core;
using System;
using System.Buffers;

namespace CK.StObj.Model
{
    /// <summary>
    /// Abstraction of any service marshall target.
    /// The only constraint is that a marshall target should have a name that identifies it.
    /// </summary>
    public interface IMarshallTarget
    {
        /// <summary>
        /// Gets the name of this target.
        /// </summary>
        string TargetName { get; }
    }

    /// <summary>
    /// Allows <see cref="IAutoService"/> implementation to be marshalled by value
    /// thanks to any serialization/deserialization mechanism.
    /// Such marshaller implementations must be registered and available in the DI container as soon
    /// as the marshallable service is used from a remote/background context.
    /// Whenever a marshaller interface is also marked as a <see cref="IAutoService"/>, its registration automatically
    /// declares the <typeparamref name="T"/> to be a Marshallable service (even if T is not marked with [IsMarshallable] attribute).
    /// </summary>
    /// <typeparam name="T">Type of the service to marshal.</typeparam>
    public interface IMarshaller<T>
    {
        /// <summary>
        /// Gets whether <typeparamref name="T"/> can be marshalled to the target.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <returns>True if the service can be marshalled, false otherwise.</returns>
        bool CanMarshallTo( IMarshallTarget target );

        /// <summary>
        /// Writes any information to the binary writer that <see cref="ReadMarshallInfo(ICKBinaryReader, IServiceProvider)"/> will use to
        /// instantiate a copy of the <paramref name="service"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="target">The marshalling target.</param>
        /// <param name="buffer">The buffer to write the marshall info to.</param>
        /// <param name="service">The service to marshal.</param>
        void WriteMarshallInfo( IActivityMonitor monitor, IMarshallTarget target, IBufferWriter<byte> buffer, T service );

        /// <summary>
        /// Reads previously written info and recreates a service instance.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="source">The source marshaller.</param>
        /// <param name="buffer">The buffer to deserialize.</param>
        /// <returns>The marshalled service.</returns>
        T ReadMarshallInfo( IActivityMonitor monitor, IMarshallTarget source, ReadOnlySequence<byte> buffer );
    }
}
