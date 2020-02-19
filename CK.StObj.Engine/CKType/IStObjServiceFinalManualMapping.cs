using CK.Core;

namespace CK.Setup
{
    interface IStObjServiceFinalManualMapping : IStObjServiceClassFactory
    {
        /// <summary>
        /// Gets the unique number that identifies this factory.
        /// </summary>
        int Number { get; }
    }

}
