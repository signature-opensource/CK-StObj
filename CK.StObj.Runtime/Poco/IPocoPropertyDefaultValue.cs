using CK.Core;
using System;

namespace CK.Setup
{
    /// <summary>
    /// Captures the [DefaultValue(...)] attribute.
    /// </summary>
    public interface IPocoPropertyDefaultValue
    {
        /// <summary>
        /// Gets the default type value (for [<see cref="DefaultValueAttribute(Type,string)"/>]).
        /// </summary>
        Type? DefaultValueType { get; }

        /// <summary>
        /// Gets the first property that defines this default value.
        /// Other properties must define the same default value.
        /// </summary>
        IPocoPropertyImpl Definer { get; }

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
