using System;
using System.Collections.Generic;

namespace CK.Core;

/// <summary>
/// Describes the final type that must be resolved and whether
/// it is a scoped or a singleton service.
/// </summary>
public interface IStObjServiceClassDescriptor : IStObjFinalClass
{
    /// <summary>
    /// Gets the service kind.
    /// </summary>
    AutoServiceKind AutoServiceKind { get; }
}
