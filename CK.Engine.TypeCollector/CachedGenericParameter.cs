using System;
using System.Reflection;

namespace CK.Engine.TypeCollector
{
    /// <summary>
    /// Captures <see cref="CachedGenericTypeDefinition.GenericParameters"/> information.
    /// </summary>
    public readonly struct CachedGenericParameter
    {
        readonly Type _type;

        internal CachedGenericParameter( Type type ) => _type = type;

        /// <summary>
        /// Gets the parameter name.
        /// </summary>
        public string Name => _type.Name;

        /// <summary>
        /// Gets the parameter attributes.
        /// </summary>
        public GenericParameterAttributes Attributes => _type.GenericParameterAttributes;
    }
}
