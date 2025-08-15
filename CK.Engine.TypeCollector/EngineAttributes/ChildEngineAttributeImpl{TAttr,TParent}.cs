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
    internal override sealed bool OnInitFields( IActivityMonitor monitor,
                                                         ICachedItem item,
                                                         EngineAttribute attribute,
                                                         EngineAttributeImpl? parentImpl )
    {
        // Use logical and (non short-circuit).
        return base.OnInitFields( monitor, item, attribute, parentImpl )
               & CheckParentType( monitor, this, typeof( TParent ), parentImpl );
    }

    /// <summary>
    /// Gets the typed parent attribute implementation.
    /// </summary>
    public new TParent ParentImpl => Unsafe.As<TParent>( base.ParentImpl! );

}
