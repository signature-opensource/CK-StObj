using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Setup.Json
{
    /// <summary>
    /// Defines basic, direct types that are directly handled.
    /// </summary>
    public enum JsonDirectType
    {
        /// <summary>
        /// Regular type (has its own Write/Read code generator).
        /// </summary>
        None,

        /// <summary>
        /// Untyped is handled by Read/WriteObject (the type appears with the value).
        /// </summary>
        Untyped,

        /// <summary>
        /// A raw string.
        /// </summary>
        String,

        /// <summary>
        /// A number is, by default, an integer.
        /// </summary>
        Number,

        /// <summary>
        /// Raw boolean type.
        /// </summary>
        Boolean
    }
}
