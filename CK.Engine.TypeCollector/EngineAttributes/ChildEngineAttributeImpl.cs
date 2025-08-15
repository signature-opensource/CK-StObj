using CK.Core;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Full strongly typed <see cref="EngineAttributeImpl"/> with a required <see cref="ParentAttribute"/>, typed attribute
/// and optional typed children.
/// <para>
/// The <typeparamref name="TParent"/> and/or <see cref="TChildren"/> can be <see cref="EngineAttributeImpl"/>.
/// </para>
/// </summary>
/// <typeparam name="TAttr">The attribute type.</typeparam>
/// <typeparam name="TParent">The expected parent's type.</typeparam>
/// <typeparam name="TChildren">The children's type.</typeparam>
public class ChildEngineAttributeImpl<TAttr,TParent,TChildren> : EngineAttributeImpl<TAttr,TChildren>
    where TAttr : class, IEngineAttribute
    where TParent : EngineAttributeImpl
    where TChildren : EngineAttributeImpl
{

    /// <summary>
    /// This calls <see cref="EngineAttributeImpl.CheckAttributeType(IActivityMonitor, EngineAttributeImpl, System.Type, System.Type)"/>
    /// and <see cref="EngineAttributeImpl.CheckParentType(IActivityMonitor, EngineAttributeImpl, System.Type, EngineAttributeImpl?)"/>
    /// helpers.
    /// </summary>
    /// <param name="monitor">The monitor to log errors.</param>
    /// <param name="item">The decorated item.</param>
    /// <param name="attribute">The attribute.</param>
    /// <param name="parentImpl">The parent implementation if the attribute is a <see cref="IChildEngineAttribute{T}"/>.</param>
    /// <returns>True on success, false on error. Errors must be logged.</returns>
    protected internal override bool OnInitFields( IActivityMonitor monitor,
                                                   ICachedItem item,
                                                   EngineAttribute attribute,
                                                   EngineAttributeImpl? parentImpl )
    {
        return base.OnInitFields( monitor, item, attribute, parentImpl )
               & CheckParentType( monitor, this, typeof( TParent ), parentImpl );
    }

    /// <summary>
    /// Gets the parent attribute implementation.
    /// </summary>
    public new TParent ParentAttribute => Unsafe.As<TParent>( base.ParentAttribute! );

}
