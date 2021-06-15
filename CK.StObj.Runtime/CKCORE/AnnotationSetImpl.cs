using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CK.Core
{
    /// <summary>
    /// Implementation of the <see cref="IAnnotationSet"/> interface.
    /// As a mutable struct, this should not be exposed directly but encapsulated
    /// in a class with a relay on its methods.
    /// <para>
    /// This is a direct copy of the <see cref="System.Xml.Linq.XObject"/> annotation implementation.
    /// </para>
    /// </summary>
    /// <remarks>
    /// The whole point of this implementation is to minimize memory footprint of such annotations,
    /// especially when they are empty or contains few items.
    /// </remarks>
    public struct AnnotationSetImpl : IAnnotationSet
    {
        object? _a;

        /// <inheritdoc />
        public void AddAnnotation( object annotation )
        {
            if( annotation == null ) throw new ArgumentNullException( nameof( annotation ) );
            if( _a == null )
            {
                _a = annotation is object[]
                        ? new object?[] { annotation }
                        : annotation;
            }
            else
            {
                if( !(_a is object?[] a) )
                {
                    _a = new object?[] { _a, annotation };
                }
                else
                {
                    int i = 0;
                    while( i < a.Length && a[i] != null ) i++;
                    if( i == a.Length )
                    {
                        Array.Resize( ref a, i * 2 );
                        _a = a;
                    }
                    a[i] = annotation;
                }
            }
        }

        /// <inheritdoc />
        public object? Annotation( Type type )
        {
            if( type == null ) throw new ArgumentNullException( nameof( type ) );
            if( _a != null )
            {
                if( !(_a is object?[] a) )
                {
                    if( IsInstanceOfType( _a, type ) ) return _a;
                }
                else
                {
                    for( int i = 0; i < a.Length; i++ )
                    {
                        object? obj = a[i];
                        if( obj == null ) break;
                        if( IsInstanceOfType( obj, type ) ) return obj;
                    }
                }
            }
            return null;
        }

        /// <inheritdoc />
        public T? Annotation<T>() where T : class
        {
            if( _a != null )
            {
                if( !(_a is object?[] a) ) return _a as T;
                for( int i = 0; i < a.Length; i++ )
                {
                    object? obj = a[i];
                    if( obj == null ) break;
                    if( obj is T result ) return result;
                }
            }
            return null;
        }

        /// <inheritdoc />
        public IEnumerable<object> Annotations( Type type )
        {
            if( type == null ) throw new ArgumentNullException( nameof( type ) );
            return AnnotationsIterator( type );
        }

        private IEnumerable<object> AnnotationsIterator( Type type )
        {
            if( _a != null )
            {
                if( !(_a is object?[] a) )
                {
                    if( IsInstanceOfType( _a, type ) ) yield return _a;
                }
                else
                {
                    for( int i = 0; i < a.Length; i++ )
                    {
                        object? obj = a[i];
                        if( obj == null ) break;
                        if( IsInstanceOfType( obj, type ) ) yield return obj;
                    }
                }
            }
        }

        /// <inheritdoc />
        public IEnumerable<T> Annotations<T>() where T : class
        {
            if( _a != null )
            {
                if( !(_a is object?[] a) )
                {
                    if( _a is T result ) yield return result;
                }
                else
                {
                    for( int i = 0; i < a.Length; i++ )
                    {
                        object? obj = a[i];
                        if( obj == null ) break;
                        if( obj is T result ) yield return result;
                    }
                }
            }
        }

        /// <inheritdoc />
        public void RemoveAnnotations( Type type )
        {
            if( type == null ) throw new ArgumentNullException( nameof( type ) );
            if( _a != null )
            {
                if( !(_a is object?[] a) )
                {
                    if( IsInstanceOfType( _a, type ) ) _a = null;
                }
                else
                {
                    int i = 0, j = 0;
                    while( i < a.Length )
                    {
                        object? obj = a[i];
                        if( obj == null ) break;
                        if( !IsInstanceOfType( obj, type ) ) a[j++] = obj;
                        i++;
                    }
                    if( j == 0 )
                    {
                        _a = null;
                    }
                    else
                    {
                        while( j < i ) a[j++] = null;
                    }
                }
            }
        }

        /// <inheritdoc />
        public void RemoveAnnotations<T>() where T : class
        {
            if( _a != null )
            {
                if( !(_a is object?[] a) )
                {
                    if( _a is T ) _a = null;
                }
                else
                {
                    int i = 0, j = 0;
                    while( i < a.Length )
                    {
                        object? obj = a[i];
                        if( obj == null ) break;
                        if( !(obj is T) ) a[j++] = obj;
                        i++;
                    }
                    if( j == 0 )
                    {
                        _a = null;
                    }
                    else
                    {
                        while( j < i ) a[j++] = null;
                    }
                }
            }
        }

        static bool IsInstanceOfType( object? o, Type type ) => o != null && type.IsAssignableFrom( o.GetType() );
    }
}
