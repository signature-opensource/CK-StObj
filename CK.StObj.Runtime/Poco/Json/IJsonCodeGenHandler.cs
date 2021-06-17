using CK.CodeGen;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Setup.Json
{
    /// <summary>
    /// The code writer delegate is in charge of generating the write code into a <see cref="System.Text.Json.Utf8JsonWriter"/>
    /// variable named "w" and a PocoJsonSerializerOptions variable named "options".
    /// <para>
    /// This is configured on JsonTypeInfo by <see cref="JsonTypeInfo.Configure(CodeWriter, CodeReader)"/> and
    /// used by handlers bound to the type when <see cref="IJsonCodeGenHandler.GenerateWrite"/> is called.
    /// </para>
    /// </summary>
    /// <param name="write">The code writer to uses.</param>
    /// <param name="variableName">The variable name to write.</param>
    public delegate void CodeWriter( ICodeWriter write, string variableName );

    /// <summary>
    /// The code reader delegate is in charge of generating the read code from a  <see cref="System.Text.Json.Utf8JsonReader"/>
    /// variable named "r" and a PocoJsonSerializerOptions variable named "options".
    /// See <see cref="CodeWriter"/>.
    /// </summary>
    /// <param name="read">The code writer to use.</param>
    /// <param name="variableName">The variable name.</param>
    /// <param name="assignOnly">True is the variable must be only assigned: no in-place read is possible.</param>
    /// <param name="isNullable">True if the variable can be null, false if it cannot be null.</param>
    public delegate void CodeReader( ICodeWriter read, string variableName, bool assignOnly, bool isNullable );


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
        /// Gets the name with '?' suffix if <see cref="IsNullable"/> is true.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the <see cref="JsonTypeInfo.ECMAScriptStandardName"/> with '?' suffix if <see cref="IsNullable"/> is true.
        /// </summary>
        string ECMAScriptStandardName { get; }

        /// <summary>
        /// Gets whether this <see cref="Name"/> differs from this <see cref="ECMAScriptStandardName"/>.
        /// </summary>
        bool HasECMAScriptStandardName { get; }

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
        /// may reference a specialization) and that it is written by the generic/untyped WriteObject.
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
        /// <param name="assignOnly">True to force the assignment of the variable, not trying to reuse it (typically because it is known to be uninitialized).</param>
        /// <param name="skipIfNullBlock">
        /// True to skip the "if( variableName == null )" block even if <see cref="IsNullable"/> is true (typically because the variable is known to be not null).
        /// </param>
        void GenerateRead( ICodeWriter read, string variableName, bool assignOnly, bool skipIfNullBlock = false );

        /// <summary>
        /// Creates a handler for type that is mapped to this one.
        /// Its <see cref="IsTypeMapping"/> is true.
        /// </summary>
        /// <param name="t">The mapped type.</param>
        /// <returns>An handler for the type.</returns>
        IJsonCodeGenHandler CreateAbstract( Type t );

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
