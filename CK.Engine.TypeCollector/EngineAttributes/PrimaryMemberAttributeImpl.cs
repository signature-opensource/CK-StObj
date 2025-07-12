using CK.Core;
using CK.Engine.TypeCollector;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace CK.Engine.TypeCollector;

public abstract class PrimaryMemberAttributeImpl : IPrimaryAttributeImpl
{
    [AllowNull] private protected ICachedMember _member;
    [AllowNull] private protected PrimaryMemberAttribute _attribute;
    internal SecondaryMemberAttributeImpl? _firstSecondary;
    internal int _secondaryCount;

    bool IAttributeImpl.InitFields( IActivityMonitor monitor, ICachedItem item, Attribute attribute )
    {
        _member = (ICachedMember)item;
        _attribute = (PrimaryMemberAttribute)attribute;
        return OnInitFields( monitor );
    }

    private protected virtual bool OnInitFields( IActivityMonitor monitor ) => true;

    /// <summary>
    /// Overridable initialization of this primary attribute implementation.
    /// <para>
    /// This calls <see cref="SecondaryMemberAttributeImpl.Initialize(IActivityMonitor)"/> on all
    /// the <see cref="SecondaryAttributes"/>: this base method MUST be called.
    /// </para>
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <returns>True on success, false on error.</returns>
    protected virtual bool Initialize( IActivityMonitor monitor )
    {
        bool success = true;
        var f = _firstSecondary;
        while( f != null )
        {
            success &= f.Initialize( monitor );
            f = f.NextSecondary;
        }
        return success;
    }

    bool IPrimaryAttributeImpl.Initialize( IActivityMonitor monitor ) => Initialize( monitor );

    internal virtual bool AddSecondary( IActivityMonitor monitor,
                                        SecondaryMemberAttributeImpl secondary,
                                        SecondaryMemberAttributeImpl? lastSecondary )
    {
        Throw.DebugAssert( lastSecondary == null || _firstSecondary != null );
        if( lastSecondary == null )
        {
            _firstSecondary = secondary;
        }
        else
        {
            lastSecondary._nextSecondary = secondary;
        }
        _secondaryCount++;
        return true;
    }

    /// <inheritdoc cref="PrimaryTypeAttributeImpl.OnInitialized(IActivityMonitor)"/>
    protected virtual bool OnInitialized( IActivityMonitor monitor ) => true;

    bool IAttributeImpl.OnInitialized( IActivityMonitor monitor ) => OnInitialized( monitor );

    /// <summary>
    /// Gets the type that declares the <see cref="Member"/>.
    /// </summary>
    public ICachedType Type => _member.DeclaringType;

    /// <summary>
    /// Gets the decorated member.
    /// </summary>
    public ICachedMember Member => _member;

    /// <inheritdoc cref="PrimaryTypeAttributeImpl.Attribute"/>
    public PrimaryMemberAttribute Attribute => _attribute;

    Attribute IPrimaryAttributeImpl.Attribute => _attribute;

    /// <inheritdoc cref="PrimaryTypeAttributeImpl.AttributeName"/>
    public ReadOnlySpan<char> AttributeName => _attribute.GetType().Name.AsSpan( ..^9 );

    /// <summary>
    /// Gets the count of secondary attributes (associated to this <see cref="Attribute"/>) that decorates this member.
    /// </summary>
    public int SecondaryCount => _secondaryCount;

    /// <summary>
    /// Gets the first secondary attribute declared on this <see cref="Member"/>, this is the head
    /// of the <see cref="SecondaryMemberAttributeImpl.NextSecondary"/> linked list. 
    /// </summary>
    public SecondaryMemberAttributeImpl? FirstSecondary => _firstSecondary;

    /// <summary>
    /// Returns all the secondary attributes declared on this <see cref="Member"/>.
    /// </summary>
    public IEnumerable<SecondaryMemberAttributeImpl> SecondaryAttributes
    {
        get
        {
            var f = _firstSecondary;
            while( f != null )
            {
                yield return f;
                f = f.NextSecondary;
            }
        }
    }
}
