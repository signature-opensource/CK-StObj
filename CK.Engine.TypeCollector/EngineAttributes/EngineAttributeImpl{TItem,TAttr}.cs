using CK.Core;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Engine attribute implementation with strongly typed <see cref="Attribute"/>.
/// </summary>
/// <typeparam name="TAttr">The attribute's type.</typeparam>
public abstract class EngineAttributeImpl<TItem, TAttr> : EngineAttributeImpl<TItem>,
                                                          IEngineAttributeImpl<TItem, TAttr>
    where TItem : class, ICachedItem
    where TAttr : class, IEngineAttribute
{
    internal override bool SetFields( IActivityMonitor monitor,
                                      ICachedItem item,
                                      IEngineAttribute attribute,
                                      EngineAttributeImpl? parentImpl )
    {
        // Use logical and (non short-circuit). This is useless here
        // (the base always returns true) but this is the pattern to use
        // everywhere.
        return base.SetFields( monitor, item, attribute, parentImpl )
               & CheckAttributeType( monitor, this, typeof( TAttr ), attribute.GetType() );
    }

    /// <inheritdoc />
    public new TAttr Attribute => Unsafe.As<TAttr>( base.Attribute );

}

