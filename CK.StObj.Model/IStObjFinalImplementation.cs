namespace CK.Core
{

    /// <summary>
    /// Associates the final, most specialized, implementation and its multiple and unique mappings.
    /// </summary>
    public interface IStObjFinalImplementation : IStObj, IStObjFinalClass
    {
        /// <summary>
        /// Gets the final implementation instance.
        /// </summary>
        object Implementation { get; }

    }

}
