using CK.Core;
using System.Collections.Generic;

namespace CK.Engine.TypeCollector;


/// <summary>
/// Abstraction for <see cref="EngineAttributeImpl{TAttr, TChildren}"/>.
/// </summary>
/// <typeparam name="TAttr">The attribute's type.</typeparam>
/// <typeparam name="TChildren">The children's type.</typeparam>
public interface IEngineAttributeImpl<TAttr, TChildren> : IEngineAttributeImpl<TAttr>
    where TAttr : class, IEngineAttribute
    where TChildren : class, IEngineAttributeImpl
{
    /// <summary>
    /// Gets the typed children's attribute.
    /// </summary>
    new IReadOnlyCollection<TChildren> ChildrenAttributes { get; }
}
