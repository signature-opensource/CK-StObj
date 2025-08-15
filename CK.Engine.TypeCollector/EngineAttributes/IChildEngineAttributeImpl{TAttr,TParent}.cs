using CK.Core;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Abstraction for <see cref="EngineAttributeImpl{TAttr}"/>.
/// </summary>
/// <typeparam name="TAttr">The attribute type.</typeparam>
/// <typeparam name="TParent">The expected parent's type.</typeparam>
public interface IChildEngineAttributeImpl<TAttr,TParent> : IEngineAttributeImpl<TAttr>
    where TAttr : class, IEngineAttribute
    where TParent : class, IEngineAttributeImpl
{
    /// <summary>
    /// Gets the parent implementation.
    /// </summary>
    new TParent ParentImpl { get; }
}
