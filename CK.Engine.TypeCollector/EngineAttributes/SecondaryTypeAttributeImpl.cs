using CK.Core;
using CK.Engine.TypeCollector;
using System;
using System.Diagnostics.CodeAnalysis;

namespace CK.Engine.TypeCollector;

public abstract class SecondaryTypeAttributeImpl : ISecondaryAttributeImpl
{
    [AllowNull] private protected ICachedType _type;
    [AllowNull] private protected PrimaryTypeAttributeImpl _primary;
    [AllowNull] private protected SecondaryTypeAttribute _attribute;
    internal SecondaryTypeAttributeImpl? _nextSecondary;

    bool IAttributeImpl.InitFields( IActivityMonitor monitor, ICachedItem item, Attribute attribute )
    {
        _type = (ICachedType)item;
        _attribute = (SecondaryTypeAttribute)attribute;
        return OnInitFields( monitor );
    }

    private protected virtual bool OnInitFields( IActivityMonitor monitor ) => true;

    bool ISecondaryAttributeImpl.SetPrimary( IActivityMonitor monitor, IPrimaryAttributeImpl primary, ISecondaryAttributeImpl? lastSecondary )
    {
        _primary = (PrimaryTypeAttributeImpl)primary;
        Throw.DebugAssert( _nextSecondary == null );
        return _primary.AddSecondary( monitor, this, (SecondaryTypeAttributeImpl?)lastSecondary );
    }

    Type ISecondaryAttributeImpl.ExpectedPrimaryType => _attribute.PrimaryType;

    /// <summary>
    /// Gets the decorated type.
    /// </summary>
    public ICachedType Type => _type;

    /// <summary>
    /// Gets the primary attribute implementation.
    /// </summary>
    public PrimaryTypeAttributeImpl Primary => _primary;

    IPrimaryAttributeImpl? ISecondaryAttributeImpl.Primary => _primary;

    /// <inheritdoc cref="PrimaryTypeAttributeImpl.Attribute"/>
    public SecondaryTypeAttribute Attribute => _attribute;

    /// <inheritdoc cref="PrimaryTypeAttributeImpl.AttributeName"/>
    public ReadOnlySpan<char> AttributeName => _attribute.GetType().Name.AsSpan( ..^9 );

    /// <summary>
    /// Gets the next secondary attribute in the <see cref="PrimaryTypeAttributeImpl.SecondaryAttributes"/>.
    /// </summary>
    public SecondaryTypeAttributeImpl? NextSecondary => _nextSecondary;

    /// <summary>
    /// Must initialize this attribute implementation.
    /// Does nothing by default (always returns true).
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <returns>True on success, false on error.</returns>
    internal protected virtual bool Initialize( IActivityMonitor monitor ) => true;

    /// <inheritdoc cref="PrimaryTypeAttributeImpl.OnInitialized(IActivityMonitor)"/>
    protected virtual bool OnInitialized( IActivityMonitor monitor ) => true;

    bool IAttributeImpl.OnInitialized( IActivityMonitor monitor ) => OnInitialized( monitor );

}
