using CK.CodeGen;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Setup.Json
{
    /// <summary>
    /// Type handler with support for nullable types (value types as well as reference types)
    /// and abstract mapping.
    /// </summary>
    public interface IJsonCodeGenHandler
    {
        /// <summary>
        /// Gets the type handled.
        /// It can differ from the <see cref="JsonTypeInfo.Type"/> if it's
        /// a value type and <see cref="IsNullable"/> is true (type is <see cref="Nullable{T}"/>)
        /// or if this <see cref="IsTypeMapping"/> is true.
        /// </summary>
        Type Type { get; }

        /// <summary>
        /// Gets the JSON (safe mode) name with '?' suffix if <see cref="IsNullable"/> is true.
        /// </summary>
        string JsonName { get; }

        /// <summary>
        /// Gets the <see cref="JsonTypeInfo.NumberName"/> with 'N' suffix if <see cref="IsNullable"/> is true.
        /// </summary>
        string NumberName => IsNullable ? TypeInfo.NumberName + "N" : TypeInfo.NumberName;

        /// <summary>
        /// Gets the <see cref="JsonTypeInfo"/>.
        /// </summary>
        JsonTypeInfo TypeInfo { get; }

        /// <summary>
        /// Gets whether this <see cref="Type"/> is not the same as the actual <see cref="TypeInfo.Type"/>
        /// and that it is not unambiguously mapped to it: the mapped type name must be written in order
        /// to resolve it.
        /// <para>
        /// Note that the type name is also written if <see cref="JsonTypeInfo.IsFinal"/> is false (since a base class
        /// may reference a specialization).
        /// </para>
        /// </summary>
        bool IsTypeMapping { get; }

        /// <summary>
        /// Gets whether this <see cref="Type"/> must be considered as a nullable one.
        /// </summary>
        bool IsNullable { get; }

        /// <summary>
        /// Generates the code required to write a value stored in <paramref name="variableName"/>.
        /// </summary>
        /// <param name="write">The code writer.</param>
        /// <param name="variableName">The variable name.</param>
        /// <param name="withType">True or false to override (<see cref="IsTypeMapping"/> || !<see cref="JsonTypeInfo.IsFinal"/>).</param>
        void GenerateWrite( ICodeWriter write, string variableName, bool? withType = null );

        /// <summary>
        /// Generates the code required to read a value into a <paramref name="variableName"/>.
        /// </summary>
        /// <param name="read">The code reader.</param>
        /// <param name="variableName">The variable name.</param>
        /// <param name="assignOnly">True to force the assignment of the variable, not trying to reuse it (typically because it is known to be uninitialized).</param>
        void GenerateRead( ICodeWriter read, string variableName, bool assignOnly );

        /// <summary>
        /// Returns either this handler or its nullable companion.
        /// </summary>
        /// <returns>The nullable handler for the type as a nullable one.</returns>
        IJsonCodeGenHandler ToNullHandler();

        /// <summary>
        /// Returns either this handler or its non nullable companion.
        /// </summary>
        /// <returns>The non nullable handler for the type.</returns>
        IJsonCodeGenHandler ToNonNullHandler();
    }
}
