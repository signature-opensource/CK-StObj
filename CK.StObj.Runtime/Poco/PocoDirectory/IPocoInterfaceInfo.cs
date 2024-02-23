using CK.Core;
using System;

namespace CK.Setup
{
    /// <summary>
    /// Associates to <see cref="IPoco"/> interface its final, unified, implementation 
    /// and its <see cref="IPocoFactory{T}"/> interface type.
    /// </summary>
    public interface IPocoInterfaceInfo : IAnnotationSet
    {
        /// <summary>
        /// Gets the IPoco interface type.
        /// </summary>
        Type PocoInterface { get; }

        /// <summary>
        /// Gets the full C# name of this interface type.
        /// </summary>
        string CSharpName { get; }

        /// <summary>
        /// Gets the concrete, final, unified Poco type information.
        /// </summary>
        IPocoFamilyInfo Family { get; }

        /// <summary>
        /// Gets the <see cref="IPocoFactory{T}"/> where T is <see cref="PocoInterface"/> type.
        /// </summary>
        Type PocoFactoryInterface { get; }

    }
}
