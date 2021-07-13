using CK.CodeGen;
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
        /// It can differ from the <see cref="JsonTypeInfo.Type"/> if nullability differs from
        /// the "null normality" or this <see cref="IsTypeMapping"/> is true.
        /// </summary>
        NullableTypeTree Type { get; }

        /// <summary>
        /// Gets the <see cref="JsonTypeInfo.NumberName"/> with 'N' suffix if <see cref="IsNullable"/> is true.
        /// </summary>
        string NumberName => IsNullable ? TypeInfo.NumberName + "N" : TypeInfo.NumberName;

        /// <summary>
        /// Gets the type name used in generated code.
        /// For value types, when <see cref="IsNullable"/> is true, the suffix '?' is appended.
        /// For reference type, it is always the oblivious <see cref="JsonTypeInfo.GenCSharpName"/>.
        /// For type mapping (that are always reference types), this is the oblivious mapped type name, not the target mapping's one.
        /// </summary>
        string GenCSharpName { get; }

        /// <summary>
        /// Gets the JSON (safe mode) name with '?' suffix if <see cref="IsNullable"/> is true.
        /// <para>
        /// This uses the ExternalNameAttribute, the type full name
        /// or a generated name for arrays, generic List, Set and Dictionary.
        /// </para>
        /// </summary>
        string JsonName { get; }

        /// <summary>
        /// Gets the previous names if any (there is no previous names for ECMA standard mode since only basic types can have a standard name).
        /// These names have a '?' suffix if <see cref="IsNullable"/> is true.
        /// </summary>
        IEnumerable<string> PreviousJsonNames { get; }

        /// <summary>
        /// Gets the JSON name used when "ECMAScript standard" is used.
        /// For non collection types, an <see cref="ECMAScriptStandardReader"/> should be registered for this name
        /// so that 'object' can be read.
        /// </summary>
        ECMAScriptStandardJsonName ECMAScriptStandardJsonName { get; }

        /// <summary>
        /// Gets whether this <see cref="JsonName"/> differs from this <see cref="Json.ECMAScriptStandardJsonName"/>.
        /// </summary>
        public bool HasECMAScriptStandardJsonName => ECMAScriptStandardJsonName.Name != JsonName;

        /// <summary>
        /// Gets the <see cref="JsonTypeInfo"/>.
        /// </summary>
        JsonTypeInfo TypeInfo { get; }

        /// <summary>
        /// Gets a handler that unambiguously handles this <see cref="Type"/>: this Type is not the same as the
        /// actual <see cref="TypeInfo.Type"/>.
        /// </summary>
        IJsonCodeGenHandler? TypeMapping { get; }

        /// <summary>
        /// Gets whether this <see cref="Type"/> must be considered as a nullable one.
        /// </summary>
        bool IsNullable { get; }

        /// <summary>
        /// Generates the code required to write a value stored in <paramref name="variableName"/>.
        /// </summary>
        /// <param name="write">The code writer.</param>
        /// <param name="variableName">The variable name.</param>
        /// <param name="withType">
        /// True or false ignores <see cref="JsonTypeInfo.IsFinal"/>. By default, when IsFinal is false (applies to
        /// reference types only) a call to the generic Write( object ) is generated.
        /// </param>
        void GenerateWrite( ICodeWriter write, string variableName, bool? withType = null );

        /// <summary>
        /// Generates the code required to read a value into a <paramref name="variableName"/>.
        /// This calls <see cref="JsonTypeInfo.GenerateRead"/> (with <see cref="IsNullable"/>) or, if
        /// <see cref="JsonTypeInfo.IsFinal"/> is false (applies to reference types only) a call
        /// to the generic ReadObject method is generated.
        /// </summary>
        /// <param name="read">The code reader.</param>
        /// <param name="variableName">The variable name.</param>
        /// <param name="assignOnly">
        /// True to force the assignment of the variable, not trying to reuse it (typically because it is known to be uninitialized).
        /// This is used for collections (that can be cleared) and Poco (that may be already instantiated).
        /// </param>
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
