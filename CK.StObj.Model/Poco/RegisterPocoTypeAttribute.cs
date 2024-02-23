using System;

namespace CK.Core
{
    /// <summary>
    /// Enables a Poco type to explicitly register a Poco compliant type that is not
    /// necessarily reachable from any <see cref="IPoco"/>.
    /// <para>
    /// This can be defined on a struct, a struct field or on a struct or IPoco property.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The type to register.</typeparam>
    [AttributeUsage( AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true )]
    public sealed class RegisterPocoTypeAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new <see cref="RegisterPocoTypeAttribute"/>.
        /// </summary>
        /// <param name="t">The type to register.</param>
        public RegisterPocoTypeAttribute( Type t ) => Type = t;

        /// <summary>
        /// Gets the type that must be registered.
        /// </summary>
        public Type Type { get; }
    }

}
