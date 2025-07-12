using CK.Core;
using System.Runtime.CompilerServices;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Specialized <see cref="SecondaryMemberAttributeImpl"/> with a strongly typed <see cref="Attribute"/>.
/// The <typeparamref name="TAttr"/> can be an attribute type or an interface that more than one attribute types implement.
/// </summary>
/// <typeparam name="TAttr">The expected type of the attribute.</typeparam>
public abstract class SecondaryMemberAttributeImpl<TAttr> : SecondaryMemberAttributeImpl where TAttr : class
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
