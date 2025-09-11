namespace CK.Setup;

/// <summary>
/// Final <see cref="EngineResult.Status"/>.
/// </summary>
public enum RunStatus
{
    /// <summary>
    /// The engine has done nothing.
    /// </summary>
    Skipped = 0,

    /// <summary>
    /// The engine succeedded.
    /// </summary>
    Succeed = 1,

    /// <summary>
    /// The engine failed.
    /// </summary>
    Failed = 2,
}
