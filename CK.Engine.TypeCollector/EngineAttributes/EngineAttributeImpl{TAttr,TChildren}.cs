using CK.Core;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CK.Engine.TypeCollector;


/// <summary>
/// Base engine implementation with strongly typed <see cref="Attribute"/> and <see cref="ChildrenAttributes"/>.
/// This base class puts no constraints on <see cref="EngineAttributeImpl.ParentAttribute"/>.
/// </summary>
/// <typeparam name="TAttr">The attribute's type.</typeparam>
/// <typeparam name="TChildren">The children's type.</typeparam>
public abstract class EngineAttributeImpl<TAttr, TChildren> : EngineAttributeImpl<TAttr>
    where TAttr : EngineAttribute
    where TChildren : EngineAttributeImpl
{
    IReadOnlyCollection<TChildren>? _children;

    /// <inheritdoc/>
    /// <remarks>
    /// Calls <see cref="EngineAttributeImpl.CheckChildType(IActivityMonitor, EngineAttributeImpl, System.Type, System.Type)"/>
    /// helper.
    /// </remarks>
    protected override bool OnAddChild( IActivityMonitor monitor, EngineAttributeImpl c )
    {
        return CheckChildType( monitor, this, typeof(TChildren), c.GetType() );
    }

    /// <summary>
    /// Gets the typed children's attribute.
    /// </summary>
    public new IReadOnlyCollection<TChildren> ChildrenAttributes => _children ??= CreateTypedChildrenAdapter<TChildren>( this );
}
