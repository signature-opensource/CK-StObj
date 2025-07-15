using CK.Core;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Base engine implementation with strongly typed <see cref="Attribute"/>.
/// </summary>
/// <typeparam name="TAttr">The attribute's type.</typeparam>
public abstract class EngineAttributeImpl<TAttr> : EngineAttributeImpl
    where TAttr : EngineAttribute
{
    /// <inheritdoc />
    /// <remarks>
    /// This calls <see cref="EngineAttributeImpl.CheckAttributeType(IActivityMonitor, EngineAttributeImpl, System.Type, System.Type)"/>
    /// helper.
    /// </remarks>
    protected internal override bool OnInitFields( IActivityMonitor monitor, ICachedItem item, EngineAttribute attribute, EngineAttributeImpl? parentImpl )
    {
        return CheckAttributeType( monitor, this, typeof( TAttr ), attribute.GetType() );
    }

    /// <summary>
    /// Gets the attribute.
    /// </summary>
    public new TAttr Attribute => Unsafe.As<TAttr>( base.Attribute );

}

