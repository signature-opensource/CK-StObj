using CK.Core;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Full strongly typed <see cref="EngineAttributeImpl"/> with a typed attribute and required <see cref="ParentAttribute"/>.
/// </summary>
/// <typeparam name="TAttr">The attribute type.</typeparam>
/// <typeparam name="TParent">The expected parent's type.</typeparam>
public class ChildEngineAttributeImpl<TAttr, TParent> : EngineAttributeImpl<TAttr>,
                                                        IChildEngineAttributeImpl<TAttr,TParent>
    where TAttr : class, IEngineAttribute
    where TParent : class, IEngineAttributeImpl
{
    [EditorBrowsable( EditorBrowsableState.Never )]
    protected override sealed bool OnInitFields( IActivityMonitor monitor,
                                                 ICachedItem item,
                                                 TAttr attribute,
                                                 EngineAttributeImpl? parentImpl )
    {
        return CheckParentType( monitor, this, typeof( TParent ), parentImpl )
               && OnInitFields( monitor, item, attribute, Unsafe.As<TParent>( parentImpl ) );
    }

    /// <inheritdoc cref="EngineAttributeImpl.OnInitFields(IActivityMonitor, ICachedItem, EngineAttribute, EngineAttributeImpl?)"/>
    protected virtual bool OnInitFields( IActivityMonitor monitor,
                                         ICachedItem item,
                                         TAttr attribute,
                                         TParent parentImpl ) => true;

    /// <summary>
    /// Gets the parent attribute implementation.
    /// </summary>
    public new TParent ParentAttribute => Unsafe.As<TParent>( base.ParentAttribute! );

}
