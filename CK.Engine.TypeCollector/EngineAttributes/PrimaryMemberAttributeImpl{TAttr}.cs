using CK.Core;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Specialized <see cref="PrimaryMemberAttributeImpl"/> with a strongly typed <see cref="Attribute"/>.
/// The <typeparamref name="TAttr"/> can be an attribute type or an interface that more than one attribute types implement.
/// </summary>
/// <typeparam name="TAttr">The expected type of the attribute.</typeparam>
public abstract class PrimaryMemberAttributeImpl<TAttr> : PrimaryMemberAttributeImpl where TAttr : class
{
    private protected override bool OnInitFields( IActivityMonitor monitor )
    {
        return PrimaryTypeAttributeImpl.CheckAttributeType( monitor, GetType(), _attribute.GetType(), typeof( TAttr ) );
    }

    /// <summary>
    /// Gets the strongly typed attribute.
    /// </summary>
    public new TAttr Attribute => Unsafe.As<TAttr>( _attribute );
}

/// <summary>
/// Specialized <see cref="PrimaryMemberAttributeImpl"/> with a strongly typed <see cref="Attribute"/>.
/// <list type="bullet">
///   <item>The <typeparamref name="TAttr"/> can be an attribute type or an interface that more than one attribute types implement.</item>
///   <item>The <typeparamref name="TSecondaryImpl"/> can be any type that the <see cref="PrimaryTypeAttributeImpl.SecondaryAttributes"/> must all implement.</item>
/// </list>
/// </summary>
/// <typeparam name="TAttr">The expected type of the attribute.</typeparam>
/// <typeparam name="TAttr">The expected type of the <see cref="PrimaryTypeAttributeImpl.SecondaryAttributes"/>.</typeparam>
public abstract class PrimaryMemberAttributeImpl<TAttr,TSecondaryImpl> : PrimaryMemberAttributeImpl<TAttr>
    where TAttr : class
    where TSecondaryImpl : class
{
    /// <inheritdoc cref="PrimaryMemberAttributeImpl.FirstSecondary"/>
    public new TSecondaryImpl? FirstSecondary => Unsafe.As<TSecondaryImpl>( _firstSecondary );

    /// <inheritdoc cref="PrimaryMemberAttributeImpl.SecondaryAttributes"/>
    public new IEnumerable<TSecondaryImpl> SecondaryAttributes => Unsafe.As<IEnumerable<TSecondaryImpl>>( base.SecondaryAttributes );

    internal override bool AddSecondary( IActivityMonitor monitor,
                                         SecondaryMemberAttributeImpl secondary,
                                         SecondaryMemberAttributeImpl? lastSecondary )
    {
        base.AddSecondary( monitor, secondary, lastSecondary );
        return PrimaryTypeAttributeImpl.CheckSecondaryType( monitor, GetType(), secondary.GetType(), typeof( TSecondaryImpl ) );
    }
}
