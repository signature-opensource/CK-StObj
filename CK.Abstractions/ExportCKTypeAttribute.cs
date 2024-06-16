using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Exposes one or more type from a referenced assembly that has been hidden by <see cref="ExcludeCKAssemblyAttribute"/>.
    /// <para>
    /// Exposed types must be public (<see cref="Type.IsVisible"/>) otherwise it is a setup error.
    /// </para>
    /// <para>
    /// Exposing types that are not from a hidden assembly or from this assembly has no effect.
    /// </para>
    /// </summary>
    [AttributeUsage( AttributeTargets.Assembly, AllowMultiple = true )]
    public sealed class ExportCKTypeAttribute : Attribute
    {
        readonly IEnumerable<Type> _types;

        /// <summary>
        /// Initializes a new <see cref="ExportCKTypeAttribute"/>.
        /// </summary>
        /// <param name="type">The first type to expose.</param>
        /// <param name="otherTypes">Other types to expose.</param>
        public ExportCKTypeAttribute( Type type, params Type[] otherTypes )
        {
            _types = otherTypes.Prepend( type );
        }

        /// <summary>
        /// Gets the types that must be exposed from this assembly even if they are
        /// defined in hidden references via <see cref="ExcludeCKAssemblyAttribute"/>.
        /// </summary>
        public IEnumerable<Type> Types => _types;

    }

}
