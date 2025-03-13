using CK.Core;
using System.Collections.Generic;

namespace CK.StObj.Engine.Tests.CrisLike;

/// <summary>
/// Simple model for errors: a list of strings.
/// </summary>
[ExternalName( "CrisSimpleError" )]
public interface ISimpleErrorResult : IPoco
{
    /// <summary>
    /// Gets the list of error strings.
    /// </summary>
    IList<string> Errors { get; }
}
