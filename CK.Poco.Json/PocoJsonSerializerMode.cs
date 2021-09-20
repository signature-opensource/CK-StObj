using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Core
{
    /// <summary>
    /// Captures serialization mode.
    /// </summary>
    public enum PocoJsonSerializerMode
    {
        /// <summary>
        /// float, single, small integers up to the Int32 are simply numbers but long (Int64), ulong (UInt64) and Decimal
        /// are expressed as strings.
        /// This is the default.
        /// </summary>
        ECMAScriptSafe,

        /// <summary>
        /// This mode introduces 2 purely client types that are "Number" and "BigInt". The float, single, small integers up to the Int32 are 
        /// exchanged as `Number` and big integers (long, ulong, decimal, BigIntegers) are exchanged as `BigInt`.
        /// </summary>
        ECMAScriptStandard
    }
}
