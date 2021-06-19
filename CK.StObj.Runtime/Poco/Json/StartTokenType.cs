using System;
using System.Collections.Generic;
using System.Text;

#pragma warning disable CA1720 // Identifier contains type name

namespace CK.Setup.Json
{
    /// <summary>
    /// Defines the System.Text.Json.JsonTokenType that starts the <see cref="JsonTypeInfo"/> representation.
    /// </summary>
    public enum StartTokenType
    {
        /// <summary>
        /// The representation starts with a String token.
        /// </summary>
        String,

        /// <summary>
        /// The representation starts with a Number token.
        /// </summary>
        Number,

        /// <summary>
        /// The representation is directly the false/true token.
        /// </summary>
        Boolean,

        /// <summary>
        /// The representation starts with a StartArray token.
        /// </summary>
        Array,

        /// <summary>
        /// The representation starts with a StartObject token.
        /// </summary>
        Object
    }
}
