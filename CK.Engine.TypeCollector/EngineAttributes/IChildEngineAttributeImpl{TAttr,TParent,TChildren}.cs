using CK.Core;
using System.Collections.Generic;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Abstraction for <see cref="ChildEngineAttributeImpl{TAttr, TParent, TChildren}"/>.
/// </summary>
public interface IChildEngineAttributeImpl<TAttr, TParent, TChildren> : IEngineAttributeImpl<TAttr, TChildren>,
                                                                        IChildEngineAttributeImpl<TAttr, TParent>
    where TAttr : class, IEngineAttribute
    where TParent : class, IEngineAttributeImpl
    where TChildren : class, IEngineAttributeImpl
{
}
