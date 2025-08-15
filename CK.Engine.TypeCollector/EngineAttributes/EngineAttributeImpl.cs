using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Base class for <see cref="EngineAttribute"/> and <see cref="EngineAttribute{T}"/> implementations.
/// <para>
/// This is the non generic base class: there are no constraints on <see cref="Attribute"/>, <see cref="ParentAttribute"/>
/// and <see cref="ChildrenAttributes"/> types. Use <see cref="EngineAttributeImpl"/>
/// </para>
/// </summary>
public abstract class EngineAttributeImpl : IEngineAttributeImpl
{
    [AllowNull] private protected ICachedItem _decoratedItem;
    [AllowNull] private protected IEngineAttribute _attribute;
    EngineAttributeImpl? _parentImpl;
    [AllowNull] IReadOnlyCollection<EngineAttributeImpl> _children;
    EngineAttributeImpl? _nextChildImpl;

    internal void SetFields( ICachedItem item,
                             IEngineAttribute attribute,
                             EngineAttributeImpl? parentImpl )
    {
        _children = ImmutableArray<EngineAttributeImpl>.Empty;
        _decoratedItem = item;
        _attribute = attribute;
        _parentImpl = parentImpl;
    }

    /// <summary>
    /// Extension point that must be used to validate item, attribute or parent.
    /// <para>
    /// By default, this always returns true.
    /// </para>
    /// <para>
    /// The strongly typed implementation like <see cref="ChildEngineAttributeImpl{TAttr, TParent}"/>
    /// handle this transparently and offers strongly typed version of this method
    /// (like <see cref="ChildEngineAttributeImpl{TAttr, TParent}.OnInitFields(IActivityMonitor, ICachedItem, TAttr, TParent)"/>).
    /// </para>
    /// </summary>
    /// <param name="monitor">The monitor to log errors.</param>
    /// <param name="item">The decorated item.</param>
    /// <param name="attribute">The attribute.</param>
    /// <param name="parentImpl">The parent implementation if the attribute is a <see cref="IChildEngineAttribute{T}"/>.</param>
    /// <returns>True on success, false on error. Errors must be logged.</returns>
    internal protected virtual bool OnInitFields( IActivityMonitor monitor,
                                                  ICachedItem item,
                                                  EngineAttribute attribute,
                                                  EngineAttributeImpl? parentImpl )
    {
        return true;
    }

    internal bool AddChild( IActivityMonitor monitor, EngineAttributeImpl c )
    {
        if( _children is not ChildrenCollection children )
        {
            _children = new ChildrenCollection( c );
        }
        else
        {
            children.Add( c );
        }
        return OnAddChild( monitor, c );
    }

    /// <summary>
    /// Extension point that must be used to validate children.
    /// <para>
    /// By default, this always returns true. 
    /// </para>
    /// </summary>
    /// <param name="monitor">The monitor to log errors.</param>
    /// <param name="c">The new children.</param>
    /// <returns>True on success, false on error. Errors must be logged.</returns>
    protected virtual bool OnAddChild( IActivityMonitor monitor, EngineAttributeImpl c )
    {
        return true;
    }

    /// <summary>
    /// Gets the decorated item.
    /// </summary>
    public ICachedItem DecoratedItem => _decoratedItem;

    /// <summary>
    /// Gets the original attribute.
    /// </summary>
    public IEngineAttribute Attribute => _attribute;

    /// <summary>
    /// Gets the attribute name without "Attribute" suffix.
    /// </summary>
    public ReadOnlySpan<char> AttributeName => _attribute.GetType().Name.AsSpan( ..^9 );

    /// <summary>
    /// Gets the parent attribute implementation if <see cref="Attribute"/> is a <see cref="ChildEngineAttribute{T}"/>.
    /// </summary>
    public IEngineAttributeImpl? ParentAttribute => _parentImpl;

    sealed class ChildrenCollection : IReadOnlyCollection<EngineAttributeImpl>
    {
        EngineAttributeImpl? _firstChildImpl;
        int _count;

        public ChildrenCollection( EngineAttributeImpl first )
        {
            _firstChildImpl = first;
            _count = 1;
        }

        public void Add( EngineAttributeImpl c )
        {
            _count++;
            if( _firstChildImpl == null )
            {
                _firstChildImpl = c;
            }
            else
            {
                // Reverted insertion.
                c._nextChildImpl = _firstChildImpl;
                _firstChildImpl = c;
            }
        }

        public int Count => _count;

        public IEnumerator<EngineAttributeImpl> GetEnumerator()
        {
            var f = _firstChildImpl;
            while( f != null )
            {
                yield return f;
                f = f._nextChildImpl;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Strongly typed collection on <see cref="ChildrenAttributes"/>.
        /// <para>
        /// </para>
        /// <see cref="CheckChildType(IActivityMonitor, EngineAttributeImpl, Type, Type)"/> must
        /// be called from <see cref="AddChild(IActivityMonitor, EngineAttributeImpl)"/> otherwise kitten will die.
        /// <para>
        /// This is required because the following is not possible:
        /// <code>
        /// public new IReadOnlyCollection<TChildren> ChildrenAttributes => Unsafe.As<IReadOnlyCollection<TChildren>>( base.ChildrenAttributes );
        /// </code>
        /// </para>
        /// </summary>
        internal sealed class Adapter<T> : IReadOnlyCollection<T> where T : class, IEngineAttributeImpl
        {
            readonly ChildrenCollection _c;

            public Adapter( ChildrenCollection c )
            {
                Throw.DebugAssert( c.Count > 0 && c.All( x => x is T ) );
                _c = c;
            }

            public int Count => _c.Count;

            public IEnumerator<T> GetEnumerator()
            {
                var f = _c._firstChildImpl;
                while( f != null )
                {
                    yield return Unsafe.As<T>( f );
                    f = f._nextChildImpl;
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => _c.GetEnumerator();
        }

    }

    /// <summary>
    /// Gets the children attribute implementations.
    /// </summary>
    public IReadOnlyCollection<IEngineAttributeImpl> ChildrenAttributes => _children;

    /// <summary>
    /// Internal only.
    /// Making it internal protected virtual seems to have no real interest and introduces
    /// a potential bug factory: this calls <see cref="Initialize(IActivityMonitor)"/> on the <see cref="ChildrenAttributes"/>:
    /// this base method MUST be called or we must add OnBefore/AfterChildrenInitialize methods.
    /// Moreover, this is called during EngineAttribute initialization which is not bound to any real engine "step".
    /// An error here would not necessarily occur during the corresponding engine step so it's better to only handle
    /// type/structure mismatch here.
    /// In this spirit, the <see cref="OnInitialized(IActivityMonitor)"/> extension point can be questioned, but we leave it
    /// here as it may be expected by developpers (and at least gives access to fully initialized engine attributes on the type).
    /// </para>
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <returns>True on success, false on error.</returns>
    internal bool Initialize( IActivityMonitor monitor )
    {
        bool success = true;
        foreach( var c in _children )
        {
            success &= c.Initialize( monitor );
        }
        return success;
    }

    /// <summary>
    /// Extension point called once all attributes implementation on the <see cref="DecoratedItem"/>
    /// have been successfully initialized.
    /// <para>
    /// Default implementation does nothing and always returns true.
    /// </para>
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <returns>True on success, false on error.</returns>
    internal protected virtual bool OnInitialized( IActivityMonitor monitor ) => true;

    /// <summary>
    /// Helper that checks an <see cref="Attribute"/> type.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="impl">The current implementation (this).</param>
    /// <param name="expectedAttrType">The expected attribute type.</param>
    /// <param name="attrType">The actual attribute type.</param>
    /// <returns>True on success, false on error.</returns>
    internal static bool CheckAttributeType( IActivityMonitor monitor,
                                             EngineAttributeImpl impl,
                                             Type expectedAttrType,
                                             Type attrType )
    {
        if( !expectedAttrType.IsAssignableFrom( attrType ) )
        {
            monitor.Error( $"Attribute implementation '{impl.GetType():N}' expects attribute type '{expectedAttrType:N}'. Got a '{attrType:N}' that is not a '{expectedAttrType.Name}'." );
            return false;
        }
        return true;
    }

    /// <summary>
    /// Helper that checks a required <see cref="ParentAttribute"/> type (a null <paramref name="parentImpl"/>
    /// logs an error and returnd false).
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="impl">The current implementation (this).</param>
    /// <param name="expectedParentType">The expected parent type.</param>
    /// <param name="parentImpl">The parent implementation. Can be null (and this is an error).</param>
    /// <returns>True on success, false on error.</returns>
    internal static bool CheckParentType( IActivityMonitor monitor,
                                          EngineAttributeImpl impl,
                                          Type expectedParentType,
                                          [NotNullWhen(true)]EngineAttributeImpl? parentImpl )
    {
        Type? actualParentType = parentImpl?.GetType();
        if( !expectedParentType.IsAssignableFrom( actualParentType ) )
        {
            monitor.Error( $"Attribute implementation '{impl.GetType():N}' expects a parent of type '{expectedParentType:N}' but got '{actualParentType:N}' that is not a '{expectedParentType.Name}'." );
            return false;
        }
        return true;
    }

    /// <summary>
    /// Helper that checks a child's type.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="impl">The current implementation (this).</param>
    /// <param name="expectedChildType">The expected child type.</param>
    /// <param name="actualChildType">The child's type to validate.</param>
    /// <returns>True on success, false on error.</returns>
    internal static bool CheckChildType( IActivityMonitor monitor,
                                         EngineAttributeImpl impl,
                                         Type expectedChildType,
                                         Type actualChildType )
    {
        if( !expectedChildType.IsAssignableFrom( actualChildType ) )
        {
            monitor.Error( $"Attribute implementation '{impl.GetType():N}' expects a child of type '{expectedChildType:N}' but got '{actualChildType:N}' that is not a '{expectedChildType.Name}'." );
            return false;
        }
        return true;
    }

    internal static IReadOnlyCollection<T> CreateTypedChildrenAdapter<T>( EngineAttributeImpl parent ) where T : class, IEngineAttributeImpl
    {
        var c = parent._children as ChildrenCollection;
        return c != null
                ? new ChildrenCollection.Adapter<T>( c )
                : ImmutableArray<T>.Empty;
    }

    void IEngineAttributeImpl.LocalImplentationOnly() {}
}
