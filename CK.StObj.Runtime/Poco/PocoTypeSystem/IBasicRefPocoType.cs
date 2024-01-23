using CK.Core;
using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Basic reference types are the wellknown types: string, <see cref="ExtendedCultureInfo"/>, <see cref="NormalizedCultureInfo"/>, <see cref="MCString"/>
    /// and <see cref="CodeString"/>.
    /// Like the other <see cref="PocoTypeKind.Basic"/> types they are registered by default.
    /// </summary>
    public interface IBasicRefPocoType : IPocoType
    {
        /// <summary>
        /// Gets the base type. This is null when this type specializes <see cref="object"/>.
        /// </summary>
        IBasicRefPocoType? BaseType {  get; }

        /// <summary>
        /// Gets the base types chain up to the one that specializes <c>object</c>.
        /// </summary>
        IEnumerable<IBasicRefPocoType> BaseTypes { get; }
    }

}
