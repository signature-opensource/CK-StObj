using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CK.Setup;

/// <summary>
/// Template method that handles serialization code: factorize <see cref="IPocoType"/> to <see cref="ExportCodeWriter"/>
/// mapping.
/// </summary>
public abstract class ExportCodeWriterMap
{
    readonly IPocoTypeNameMap _nameMap;
    readonly ExportCodeWriter[] _writers;
    ExportCodeWriter? _anyCodeWriter;
    readonly Dictionary<object, ExportCodeWriter> _keyedWriters;
    ExportCodeWriter? _last;
    int _writerCount;

    /// <summary>
    /// Initializes a new writer map.
    /// </summary>
    /// <param name="nameMap"></param>
    public ExportCodeWriterMap( IPocoTypeNameMap nameMap )
    {
        _nameMap = nameMap;
        _writers = new ExportCodeWriter[nameMap.TypeSystem.AllNonNullableTypes.Count];
        _keyedWriters = new Dictionary<object, ExportCodeWriter>();
    }

    /// <summary>
    /// Gets the name map bound to the serializable set of types.
    /// </summary>
    public IPocoTypeNameMap NameMap => _nameMap;

    /// <summary>
    /// Gets the <see cref="ExportCodeWriter"/> to use for untyped object.
    /// This is available even if the <see cref="PocoTypeKind.Any"/> is not in the <see cref="NameMap"/>.
    /// </summary>
    /// <returns>The writer to use.</returns>
    public ExportCodeWriter GetAnyWriter()
    {
        if( _anyCodeWriter == null )
        {
            var w = CreateAnyWriter();
            if( w == null )
            {
                Throw.InvalidOperationException( "CreateAnyWriter returned null." );
            }
            RegisterWriter( w );
            w._index = -1;
            _anyCodeWriter = w;
        }
        return _anyCodeWriter;
    }

    void RegisterWriter( ExportCodeWriter w )
    {
        Throw.DebugAssert( w._index == 0 && w._prev == null );
        w._prev = _last;
        _last = w;
        // Must start at 1 to detect brand new writers.
        w._index = ++_writerCount;
    }

    /// <summary>
    /// Gets the writer to use for a given type. The <paramref name="type"/> must be
    /// in the <see cref="NameMap"/> otherwise an <see cref="ArgumentException"/> is thrown.
    /// </summary>
    /// <param name="type">The type to handle.</param>
    /// <returns>The writer to use.</returns>
    public ExportCodeWriter GetWriter( IPocoType type )
    {
        Throw.CheckNotNullArgument( type );
        type = type.NonNullable;
        var w = _writers[type.Index >> 1];
        if( w == null )
        {
            Throw.DebugAssert( "PocoTypeSet filters out ImplementationLess types.", !type.ImplementationLess );
            // The PocoTypeKind.Any may not exist in the set. It's the reason why we implement it explicitly.
            // The any writer can always be retrieved thanks to PocoTypeKind.Any.
            if( type.Kind == PocoTypeKind.Any )
            {
                w = GetAnyWriter();
            }
            else
            {
                Throw.CheckArgument( NameMap.TypeSet.Contains( type ) );
                w = GetOrCreateWriter( type );
                if( w == null )
                {
                    Throw.InvalidOperationException( $"CreateWriter for type '{type}' returned null." );
                }
                if( w._index == 0 ) RegisterWriter( w );
            }
            _writers[type.Index >> 1] = w;
        }
        w._handledTypes?.Add( type );
        return w;
    }

    /// <summary>
    /// Gets a writer identified by a key.
    /// </summary>
    /// <param name="key">The required unique key that identifies the writer.</param>
    /// <param name="type">One of the type that can be handled by the keyed writer.</param>
    /// <param name="factory">Factory methid that will be called for the first missing key.</param>
    /// <returns>The writer.</returns>
    public ExportCodeWriter GetWriter( object key, IPocoType type, Func<object,IPocoType,ExportCodeWriter> factory )
    {
        Throw.CheckNotNullArgument( key );
        ExportCodeWriter? w;
        if( key is IPocoType t )
        {
            w = GetWriter( t );
            if( t != type ) w._handledTypes?.Add( type );
            return w;
        }
        if( !_keyedWriters.TryGetValue( key, out w ) )
        {
            w = factory( key, type );
            if( w == null )
            {
                Throw.InvalidOperationException( $"Keyed writer factory for key '{key}' returned null." );
            }
            if( !key.Equals( w.Key ) )
            {
                Throw.InvalidOperationException( $"Keyed writer factory returned a writer with the key '{w.Key}' that differs from the requested '{key}'." );
            }
            if( w._index != 0 )
            {
                Throw.InvalidOperationException( "Invaid reuse of a writer." );
            }
            RegisterWriter( w );
            _keyedWriters.Add( key, w );
        }
        w._handledTypes?.Add( type );
        return w;
    }

    /// <summary>
    /// Must be overridden to return a writer for the provided type.
    /// </summary>
    /// <param name="t">The type to handle.</param>
    /// <returns>The writer to use.</returns>
    protected abstract ExportCodeWriter GetOrCreateWriter( IPocoType t );

    /// <summary>
    /// Must be overridden to return a <see cref="ExportCodeWriter"/> that implements the
    /// code required to write an untyped <see cref="object"/>.
    /// </summary>
    /// <returns>The generic writer to use for untyped object.</returns>
    protected abstract ExportCodeWriter CreateAnyWriter();

    /// <summary>
    /// Runs this generator.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="generationContext">The current code generation context.</param>
    /// <param name="exporterType">The exporter type.</param>
    /// <param name="pocoDirectoryType">The PocoDirectory_CK.</param>
    /// <returns></returns>
    public virtual bool Run( IActivityMonitor monitor,
                             ICSCodeGenerationContext generationContext,
                             ITypeScope exporterType,
                             ITypeScope pocoDirectoryType )
    {
        using( monitor.OpenInfo( $"Running {GetType().Name} for {_nameMap.TypeSet.NonNullableTypes.Count} serializable types." ) )
        {
            try
            {
                foreach( var t in _nameMap.TypeSet.NonNullableTypes )
                {
                    GetWriter( t );
                }
                // Ensures that the Any writer is available (the PocoTypeKind.Any may not appear in the set).
                GetAnyWriter();
                var w = _last;
                while( w != null )
                {
                    w.GenerateSupportCode( monitor, generationContext, this, exporterType, pocoDirectoryType );
                    w = w._prev;
                }
                monitor.CloseGroup( $"{_writerCount} writers generated." );
                return true;
            }
            catch( Exception e )
            {
                monitor.Fatal( e );
                return false;
            }
        }
    }
}
