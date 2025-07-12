using CK.Core;
using System;
using System.Diagnostics.CodeAnalysis;

namespace CK.Engine.TypeCollector;

public abstract class SecondaryMemberAttributeImpl : ISecondaryAttributeImpl
{ 
    [AllowNull] private protected ICachedMember _member;
    [AllowNull] private protected PrimaryMemberAttributeImpl _primary;
    [AllowNull] private protected SecondaryMemberAttribute _attribute;
    internal SecondaryMemberAttributeImpl? _nextSecondary;

    bool IAttributeImpl.InitFields( IActivityMonitor monitor, ICachedItem item, Attribute attribute )
    {
        _member = (ICachedMember)item;
        _attribute = (SecondaryMemberAttribute)attribute;
        return OnInitFields( monitor );
    }

    private protected virtual bool OnInitFields( IActivityMonitor monitor ) => true;

    bool ISecondaryAttributeImpl.SetPrimary( IActivityMonitor monitor, IPrimaryAttributeImpl primary, ISecondaryAttributeImpl? lastSecondary )
    {
        _primary = (PrimaryMemberAttributeImpl)primary;
        Throw.DebugAssert( _nextSecondary == null );
        return _primary.AddSecondary( monitor, this, (SecondaryMemberAttributeImpl?)lastSecondary );
    }

    Type ISecondaryAttributeImpl.ExpectedPrimaryType => _attribute.PrimaryType;

    /// <summary>
    /// Gets the decorated member.
    /// </summary>
    public ICachedMember Member => _member;

    /// <summary>
    /// Gets the primary attribute implementation.
    /// </summary>
    public PrimaryMemberAttributeImpl Primary => _primary;

    IPrimaryAttributeImpl? ISecondaryAttributeImpl.Primary => _primary;

    /// <inheritdoc cref="PrimaryTypeAttributeImpl.Attribute"/>
    public SecondaryMemberAttribute Attribute => _attribute;

    /// <inheritdoc cref="PrimaryTypeAttributeImpl.AttributeName"/>
    public ReadOnlySpan<char> AttributeName => _attribute.GetType().Name.AsSpan( ..^9 );

    /// <summary>
    /// Gets the next secondary attribute in the <see cref="PrimaryMemberAttributeImpl.SecondaryAttributes"/>.
    /// </summary>
    public SecondaryMemberAttributeImpl? NextSecondary => _nextSecondary;

    /// <inheritdoc cref="PrimaryTypeAttributeImpl.Initialize(IActivityMonitor)"/>
    internal protected virtual bool Initialize( IActivityMonitor monitor ) => true;

    /// <inheritdoc cref="PrimaryTypeAttributeImpl.OnInitialized(IActivityMonitor)"/>
    protected virtual bool OnInitialized( IActivityMonitor monitor ) => true;

    bool IAttributeImpl.OnInitialized( IActivityMonitor monitor ) => OnInitialized( monitor );

}
