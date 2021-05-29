using CK.CodeGen;
using System;

#nullable enable

namespace CK.Setup
{
    public partial class PocoJsonSerializerImpl
    {
        /// <summary>
        /// Type handler with support for nullable types (value types as well as reference types)
        /// and abstract mapping.
        /// </summary>
        public interface IHandler
        {
            /// <summary>
            /// Gets the type handled.
            /// It can differ from the <see cref="TypeInfo.Type"/> if it's
            /// a value type and <see cref="IsNullable"/> is true (type is <see cref="Nullable{T}"/>)
            /// or if this <see cref="IsAbstractType"/> is true.
            /// </summary>
            Type Type { get; }

            /// <summary>
            /// Gets the name with '?' suffix if <see cref="IsNullable"/> is true.
            /// </summary>
            string Name { get; }

            /// <summary>
            /// Gets the <see cref="TypeInfo"/>.
            /// </summary>
            TypeInfo Info { get; }

            /// <summary>
            /// Gets whether this <see cref="Type"/> is not the same as the actual <see cref="TypeInfo.Type"/>
            /// and that it is not unambiguously mapped to it: the actual type name must be written in order
            /// to resolve it.
            /// </summary>
            bool IsAbstractType { get; }

            /// <summary>
            /// Gets whether this <see cref="Type"/> must be considered as a nullable one.
            /// </summary>
            bool IsNullable { get; }

            /// <summary>
            /// Generates the code required to write a value stored in <paramref name="variableName"/>.
            /// </summary>
            /// <param name="write">The code writer.</param>
            /// <param name="variableName">The variable name.</param>
            /// <param name="withType">True or false to override <see cref="IsAbstractType"/>.</param>
            /// <param name="skipIfNullBlock">
            /// True to skip the "if( variableName == null )" block whenever <see cref="IsNullable"/> is true.
            /// This <see cref="Type"/> and <see cref="Name"/> are kept as-is.
            /// </param>
            void GenerateWrite( ICodeWriter write, string variableName, bool? withType = null, bool skipIfNullBlock = false );

            /// <summary>
            /// Generates the code required to read a value into a <paramref name="variableName"/>.
            /// </summary>
            /// <param name="read">The code reader.</param>
            /// <param name="variableName">The variable name.</param>
            /// <param name="assignOnly">True to force the assignment of the variable, not trying to reuse it (typically because it is not initialized).</param>
            /// <param name="skipIfNullBlock">
            /// True to skip the "if( variableName == null )" block whenever <see cref="IsNullable"/> is true.
            /// </param>
            void GenerateRead( ICodeWriter read, string variableName, bool assignOnly, bool skipIfNullBlock = false );

            /// <summary>
            /// Creates a handler for type that is mapped to this one.
            /// Its <see cref="IsAbstractType"/> is true.
            /// </summary>
            /// <param name="t">The mapped type.</param>
            /// <returns>An handler for the type.</returns>
            IHandler CreateAbstract( Type t );

            /// <summary>
            /// Returns either this handler or its nullable companion.
            /// </summary>
            /// <returns>The nullable handler for the type as a nullable one.</returns>
            IHandler ToNullHandler();

            /// <summary>
            /// Returns either this handler or its non nullable companion.
            /// </summary>
            /// <returns>The non nullable handler for the type.</returns>
            IHandler ToNonNullHandler();
        }

    }
}
