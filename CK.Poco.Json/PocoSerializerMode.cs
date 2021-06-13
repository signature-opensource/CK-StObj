using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Core
{
    /// <summary>
    /// Captures serialization mode.
    /// </summary>
    public enum PocoSerializerMode
    {
        /// <summary>
        /// Not applicable.
        /// </summary>
        None,

        /// <summary>
        /// Simple JSON is used: numbers are expressed as-is since JSON does not limit number representations.
        /// Note that <see cref="System.Numerics.BigInteger"/> is however expressed as string since there's no
        /// direct support for it and we handle its serialization manually.
        /// This is the default.
        /// </summary>
        Server,

        /// <summary>
        /// float, single, small integers up to the Int32 are simply numbers but long (Int64), ulong (UInt64) and Decimal
        /// are expressed as strings.
        /// </summary>
        ECMAScriptSafe,

        /// <summary>
        /// This mode introduces 2 purely client types that are "Number" and "BigInt". The float, single, small integers up to the Int32 are 
        /// exchanged as `Number` and big integers (long, ulong, BigIntegers) are exchanged as `BigInt`.
        /// </summary>
        ECMAScriptStandard
    }
}
