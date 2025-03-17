using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.StObj.Engine.Tests.CrisLike;

/// <summary>
/// Describes command properties and its unique and zero-based index in a context.
/// This is simplified model since we ignore command handlers here, we just consider the ICommand
/// specialized poco.
/// </summary>
public interface ICrisPocoModel
{
    /// <summary>
    /// Gets the command type: this is the final type that implements the <see cref="IPoco"/> command.
    /// </summary>
    Type CommandType { get; }

    /// <summary>
    /// Creates a new Cris object.
    /// </summary>
    ICrisPoco Create();

    /// <summary>
    /// Gets the command index.
    /// </summary>
    int CommandIdx { get; }

    /// <summary>
    /// Gets the command name.
    /// </summary>
    string CommandName { get; }

    /// <summary>
    /// Gets the command previous names if any.
    /// </summary>
    IReadOnlyList<string> PreviousNames { get; }

}
