using CK.Core;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Base engine implementation with strongly typed <see cref="Attribute"/>.
/// </summary>
/// <typeparam name="TAttr">The attribute's type.</typeparam>
public abstract class EngineAttributeImpl<TAttr> : EngineAttributeImpl,
                                                   IEngineAttributeImpl<TAttr>
    where TAttr : class, IEngineAttribute
{
    [EditorBrowsable( EditorBrowsableState.Never )]
    protected internal override sealed bool OnInitFields( IActivityMonitor monitor, ICachedItem item, EngineAttribute attribute, EngineAttributeImpl? parentImpl )
    {
        return CheckAttributeType( monitor, this, typeof( TAttr ), attribute.GetType() )
               && OnInitFields( monitor, item, Unsafe.As<TAttr>( attribute ), parentImpl );
    }

    /// <inheritdoc cref="EngineAttributeImpl.OnInitFields(IActivityMonitor, ICachedItem, EngineAttribute, EngineAttributeImpl?)"/>
    protected virtual bool OnInitFields( IActivityMonitor monitor, ICachedItem item, TAttr attribute, EngineAttributeImpl? parentImpl ) => true;

    /// <inheritdoc />
    public new TAttr Attribute => Unsafe.As<TAttr>( base.Attribute );

}

