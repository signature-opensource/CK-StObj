using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.CrisLike
{
    /// <summary>
    /// Simple model for errors: a list of strings.
    /// </summary>
    [ExternalName( "CrisResultError" )]
    public interface ICrisResultError : IPoco
    {
        /// <summary>
        /// Gets the list of error strings.
        /// </summary>
        List<string> Errors { get; }
    }
}
