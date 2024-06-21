using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Declares one or more types from a referenced assembly that could have been hidden by <see cref="ExcludePFeatureAttribute"/>
    /// or by a <see cref="ExcludeCKTypeAttribute"/> in referenced assemblies.
    /// <list type="bullet">
    ///     <item>The types to register must be public (<see cref="Type.IsVisible"/>) otherwise it is a setup error.</item>
    ///     <item>They can belong to any assembly (excluded or not).</item>
    /// </list>
    /// </summary>
    [AttributeUsage( AttributeTargets.Assembly, AllowMultiple = true )]
    public sealed class RegisterCKTypeAttribute : Attribute
    {
        readonly IEnumerable<Type> _types;

        /// <summary>
        /// Initializes a new <see cref="RegisterCKTypeAttribute"/>.
        /// </summary>
        /// <param name="type">The first type to expose.</param>
        /// <param name="otherTypes">Other types to expose.</param>
        public RegisterCKTypeAttribute( Type type, params Type[] otherTypes )
        {
            _types = otherTypes.Prepend( type );
        }

        /// <summary>
        /// Gets the types that must be exposed from this assembly.
        /// </summary>
        public IEnumerable<Type> Types => _types;

    }
}
