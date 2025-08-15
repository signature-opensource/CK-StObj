using CK.Core;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CK.Engine.TypeCollector;


/// <summary>
/// Full strongly typed <see cref="EngineAttributeImpl"/> with a typed attribute, required <see cref="ParentAttribute"/> 
/// and typed <see cref="Chil"/>.
/// <para>
/// The <typeparamref name="TParent"/> and/or <see cref="TChildren"/> can be <see cref="EngineAttributeImpl"/>.
/// </para>
/// </summary>
/// <typeparam name="TAttr">The attribute type.</typeparam>
/// <typeparam name="TParent">The expected parent's type.</typeparam>
/// <typeparam name="TChildren">The children's type.</typeparam>
public class ChildEngineAttributeImpl<TAttr, TParent, TChildren> : ChildEngineAttributeImpl<TAttr, TParent>,
                                                                   IChildEngineAttributeImpl<TAttr, TParent, TChildren>
    where TAttr : class, IEngineAttribute
    where TParent : class, IEngineAttributeImpl
    where TChildren : class, IEngineAttributeImpl
{
    IReadOnlyCollection<TChildren>? _children;

    internal override sealed bool OnAddChild( IActivityMonitor monitor, EngineAttributeImpl c )
    {
        return CheckChildType( monitor, this, typeof( TChildren ), c.GetType() );
    }

    /// <inheritdoc />
    public new IReadOnlyCollection<TChildren> ChildrenImpl => _children ??= CreateTypedChildrenAdapter<TChildren>( this );


}
