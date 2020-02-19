using System;

namespace CK.Core
{
    /// <summary>
    /// Describes the final type that must be resolved and whether
    /// it is a scoped or a singleton service.
    /// </summary>
    public interface IStObjServiceClassDescriptor
    {
        /// <summary>
        /// Gets the actual Type that must be instanciated.
        /// This Type has, by design, one and only one public constructor
        /// (see <see cref="StObjServiceClassDescriptorExtension.GetSingleConstructor"/>).
        /// </summary>
        Type ClassType { get; }

        /// <summary>
        /// Gets whether this is a scoped service or a singleton one.
        /// </summary>
        bool IsScoped { get; }

    }

}
