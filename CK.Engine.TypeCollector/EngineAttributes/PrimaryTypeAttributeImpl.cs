using CK.Core;
using CK.Engine.TypeCollector;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace CK.Engine.TypeCollector;

public abstract class PrimaryTypeAttributeImpl : IPrimaryAttributeImpl
{
    [AllowNull] private protected ICachedType _type;
    [AllowNull] private protected PrimaryTypeAttribute _attribute;
    SecondaryTypeAttributeImpl? _firstSecondary;
    int _secondaryCount;

    bool IAttributeImpl.InitFields( IActivityMonitor monitor, ICachedItem item, Attribute attribute )
    {
        _type = (ICachedType)item;
        _attribute = (PrimaryTypeAttribute)attribute;
        return OnInitFields( monitor );
    }

    private protected virtual bool OnInitFields( IActivityMonitor monitor ) => true;

    /// <summary>
    /// Gets the decorated type.
    /// </summary>
    public ICachedType Type => _type;

    /// <summary>
    /// Gets the original attribute.
    /// </summary>
    public PrimaryTypeAttribute Attribute => _attribute;

    Attribute IPrimaryAttributeImpl.Attribute => _attribute;

    /// <summary>
    /// Gets the attribute name without "Attribute" suffix.
    /// </summary>
    public ReadOnlySpan<char> AttributeName => _attribute.GetType().Name.AsSpan( ..^9 );

    /// <summary>
    /// Gets the count of secondary attributes (associated to this <see cref="Attribute"/>) that decorates this type.
    /// </summary>
    public int SecondaryCount => _secondaryCount;

    /// <summary>
    /// Gets the first secondary attribute declared on this <see cref="Type"/>, this is the head
    /// of the <see cref="SecondaryTypeAttributeImpl.NextSecondary"/> linked list. 
    /// </summary>
    public SecondaryTypeAttributeImpl? FirstSecondary => _firstSecondary;

    /// <summary>
    /// Returns all the secondary attributes (associated to this <see cref="Attribute"/>) declared on this <see cref="Type"/>.
    /// </summary>
    public IEnumerable<SecondaryTypeAttributeImpl> SecondaryAttributes
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

    /// <summary>
    /// Overridable initialization of this primary attribute implementation.
    /// <para>
    /// This calls <see cref="SecondaryTypeAttributeImpl.Initialize(IActivityMonitor)"/> on all
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
                                        SecondaryTypeAttributeImpl secondary,
                                        SecondaryTypeAttributeImpl? lastSecondary )
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

    internal static bool CheckAttributeType( IActivityMonitor monitor, Type implType, Type attrType, Type expectedAttrType )
    {
        if( !expectedAttrType.IsAssignableFrom( attrType ) )
        {
            monitor.Error( $"Attribute implementation '{implType:N}' expects attribute type '{expectedAttrType:N}'. Got a '{attrType:N}' that is not a '{expectedAttrType.Name}'." );
            return false;
        }
        return true;
    }

    internal static bool CheckSecondaryType( IActivityMonitor monitor, Type implType, Type secondaryType, Type expectedSecondaryType )
    {
        if( !expectedSecondaryType.IsAssignableFrom( secondaryType ) )
        {
            monitor.Error( $"Primary attribute implementation '{implType:N}' expects secondary attribute type '{expectedSecondaryType:N}'. Got a '{secondaryType:N}' that is not a '{expectedSecondaryType.Name}'." );
            return false;
        }
        return true;
    }

    /// <summary>
    /// Extension point called once all other attributes implementation on the <see cref="Type"/>
    /// have been successfully initialized.
    /// <para>
    /// Default implementation does nothing and always returns true.
    /// </para>
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <returns>True on success, false on error.</returns>
    protected virtual bool OnInitialized( IActivityMonitor monitor ) => true;

    bool IAttributeImpl.OnInitialized( IActivityMonitor monitor ) => OnInitialized( monitor );

}
