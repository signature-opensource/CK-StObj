using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Setup
{
    /// <summary>
    /// Defines a type that must be generated in order to support
    /// the actual types.
    /// <para>
    /// These types are generated in <see cref="Namespace"/>.
    /// </para>
    /// </summary>
    public abstract class PocoRequiredSupportType
    {
        public const string Namespace = "CK.GRSupport";

        protected PocoRequiredSupportType( string typeName )
        {
            Throw.CheckNotNullOrWhiteSpaceArgument( typeName );
            TypeName = typeName;
            FullName = $"{Namespace}.{typeName}";
        }

        /// <summary>
        /// Gets the type name that must be generated.
        /// </summary>
        public string TypeName { get; }

        /// <summary>
        /// Gets the full type name that must be generated: <see cref="Namespace"/>.<see cref="TypeName"/>.
        /// </summary>
        public string FullName { get; }
    }
}
