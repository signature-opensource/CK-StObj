using System;

namespace CK.Core
{

    /// <summary>
    /// Associates the final, most specialized, implementation and its multiple and unique mappings.
    /// </summary>
    public interface IStObjFinalImplementation : IStObj, IStObjFinalClass
    {
        /// <summary>
        /// Gets the type of the most specialized implementation (mat be abstract):
        /// use <see cref="IStObjFinalClass.FinalType"/> to obtain the type that may have been generated.
        /// </summary>
        new Type ClassType { get; }

        /// <summary>
        /// Gets the final implementation instance.
        /// </summary>
        object Implementation { get; }

    }

}
