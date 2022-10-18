using System;
using System.ComponentModel;

namespace CK.Setup
{
    /// <summary>
    /// Captures the <c>[DefaultValue(...)]</c> attribute or default parameter value
    /// of record struct.
    /// </summary>
    public interface IPocoFieldDefaultValue
    {
        /// <summary>
        /// Gets the default type value (for [<see cref="DefaultValueAttribute(Type,string)"/>]).
        /// </summary>
        Type? DefaultValueType { get; }

        /// <summary>
        /// Gets the default value.
        /// </summary>
        object? Value { get; }

        /// <summary>
        /// Gets the default value in C# source code.
        /// </summary>
        string ValueCSharpSource { get; }
    }
}
