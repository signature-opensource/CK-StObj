using System;

namespace CK.Core
{
    /// <summary>
    /// Extends the descriptive <see cref="IStObjServiceClassFactoryInfo"/> with a concrete
    /// implementation of a factory method based on an external <see cref="IServiceProvider"/>.
    /// </summary>
    public interface IStObjServiceClassFactory : IStObjServiceClassFactoryInfo
    {
        /// <summary>
        /// Actual object factory.
        /// </summary>
        /// <param name="provider">The current service provider to use.</param>
        /// <returns>The created instance.</returns>
        object CreateInstance( IServiceProvider provider );
    }
}
