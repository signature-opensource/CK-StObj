using System;

namespace CK.Core
{
    /// <summary>
    /// Associates the final, most specialized, implementation and its multiple and unique mappings
    /// since this is a <see cref="IStObjFinalClass"/>.
    /// </summary>
    public interface IStObjFinalImplementation : IStObj, IStObjFinalClass
    {
        /// <summary>
        /// Gets the type of the most specialized implementation (may be abstract):
        /// use <see cref="IStObjFinalClass.FinalType"/> to obtain the type that may have been generated.
        /// </summary>
        new Type ClassType { get; }

        /// <summary>
        /// Gets the final implementation instance.
        /// </summary>
        object Implementation { get; }
    }

}
