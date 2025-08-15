using CK.Core;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Abstraction for <see cref="EngineAttributeImpl{TAttr}"/>.
/// </summary>
/// <typeparam name="TAttr">The attribute's type.</typeparam>
public interface IEngineAttributeImpl<TAttr> : IEngineAttributeImpl
    where TAttr : class, IEngineAttribute
{
    /// <inheritdoc cref="IEngineAttributeImpl.Attribute"/>
    new TAttr Attribute { get; }
}
