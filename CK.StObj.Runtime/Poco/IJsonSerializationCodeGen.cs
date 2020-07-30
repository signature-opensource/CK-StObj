using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Setup
{
    /// <summary>
    /// Provides services related to Json serialization support.
    /// </summary>
    public interface IJsonSerializationCodeGen
    {
        /// <summary>
        /// Registers a type so that it is known to the serializer.
        /// Supported types are <see cref="Enum"/>, <see cref="List{T}"/>, <see cref="IList{T}"/>, <see cref="ISet{T}"/>,
        /// <see cref="HashSet{T}"/>, <see cref="Dictionary{TKey, TValue}"/> and <see cref="IDictionary{TKey, TValue}"/> where
        /// the generic type must itself be known (see <see cref="IsKnownType(Type)"/>).
        /// </summary>
        /// <param name="t">The type to register.</param>
        void RegisterEnumOrCollectionType( Type t );

        /// <summary>
        /// Gets whether a type is known.
        /// </summary>
        /// <param name="t">The type.</param>
        /// <returns>True if the type is known, false otherwise.</returns>
        bool IsKnownType( Type t );
    }
}
