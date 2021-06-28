using CK.CodeGen;
using CK.Core;
using CK.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CK.Setup.Json
{

    /// <summary>
    /// Provides services related to Json serialization support.
    /// <para>
    /// This service supports automatic code generation for enums or collections types (as long as they are
    /// allowed thanks to <see cref="GetHandler(Type, bool?)"/>).
    /// Other type must be registered with their <see cref="CodeWriter"/> and <see cref="CodeReader"/> generators.
    /// </para>
    /// This service is instantiated and registered by the Engine's PocoJsonSerializerImpl that is
    /// activated by the CK.Poco.Json package (thanks to the PocoJsonSerializer static class).
    /// </para>
    /// </summary>
    public partial class JsonSerializationCodeGen
    {
        readonly IActivityMonitor _monitor;
        readonly ITypeScope _pocoDirectory;
        readonly Stack<Type> _reentrancy;
        // Used to defer calls to GenerateRead/Write.
        readonly List<Action<IActivityMonitor>> _finalReadWrite;

        /// <summary>
        /// Maps Type and Names (current and previous) to its handler.
        /// </summary>
        readonly Dictionary<object, IJsonCodeGenHandler> _map;
        // The JsonTypeInfo list is ordered. UntypedObject, value types, sealed classes and Poco
        // come first. Then come the "external" reference types ordered from "less IsAssignableFrom" (specialization)
        // to "most IsAssignableFrom" (generalization) so that switch case entries are correctly ordered.
        // This sort by insertion is done in the AllowTypeInfo method and this is also where
        // the TypeSpecOrder is computed and JsonTypeInfo.Specializations are added.
        readonly List<JsonTypeInfo> _typeInfos;
        // This is the index in the _typeInfos list from which "external" reference types must be sorted.
        // Before this mark, there are the Value Types and the Untyped (object): InitializeMap
        // starts to count.
        int _typeInfoRefTypeStartIdx;

        int _typeInfoAutoNumber;

        // The list of readers for "ECMAScript standard" mode. Initialized with "BigInt" and "Number".
        List<ECMAScriptStandardReader> _standardReaders;
        bool? _finalizedCall;

        /// <summary>
        /// Initializes a new <see cref="JsonSerializationCodeGen"/>.
        /// Basic types are immediately allowed.
        /// </summary>
        /// <param name="monitor">The monitor to capture.</param>
        /// <param name="pocoDirectory">The poco directory object.</param>
        public JsonSerializationCodeGen( IActivityMonitor monitor, ITypeScope pocoDirectory )
        {
            _monitor = monitor;
            _pocoDirectory = pocoDirectory;
            _map = new Dictionary<object, IJsonCodeGenHandler>();
            _typeInfos = new List<JsonTypeInfo>();
            _reentrancy = new Stack<Type>();
            _finalReadWrite = new List<Action<IActivityMonitor>>();
            _standardReaders = new List<ECMAScriptStandardReader>();
            InitializeBasicTypes();
        }

        /// <summary>
        /// Gets whether a type is allowed.
        /// </summary>
        /// <param name="t">The type.</param>
        /// <returns>True if the type has been allowed, false otherwise.</returns>
        public bool IsAllowedType( Type t ) => _map.ContainsKey( t );

        /// <summary>
        /// Gets the types or names mapping to the <see cref="IJsonCodeGenHandler"/> to use (keys are either the Type
        /// object or the serialized type name or appear in the <see cref="ExternalNameAttribute.PreviousNames"/>).
        /// </summary>
        public IReadOnlyDictionary<object, IJsonCodeGenHandler> HandlerMap => _map;

        /// <summary>
        /// Gets the currently allowed types.
        /// </summary>
        public IReadOnlyList<JsonTypeInfo> AllowedTypes => _typeInfos;

        /// <summary>
        /// Gets whether the <see cref="FinalizeCodeGeneration(IActivityMonitor)"/> has been called:
        /// <see cref="AllowedTypes"/> must no more be called since all the known types have been
        /// considered.
        /// </summary>
        public bool IsFinalized => _finalizedCall.HasValue;

        /// <summary>
        /// Factory for <see cref="JsonTypeInfo"/>: its <see cref="JsonTypeInfo.NumberName"/> is unique.
        /// This should be called only once per type (that must not be <see cref="Nullable{T}"/>).
        /// </summary>
        /// <param name="t">The type.</param>
        /// <param name="name">The serialized type name.</param>
        /// <param name="startTokenType">The start token type.</param>
        /// <param name="previousNames">Optional previous names.</param>
        /// <returns>A new type info.</returns>
        public JsonTypeInfo CreateTypeInfo( Type t, string name, StartTokenType startTokenType, IReadOnlyList<string>? previousNames = null )
        {
            if( t == null || (t.IsValueType && Nullable.GetUnderlyingType( t ) != null) ) throw new ArgumentException( "Must not be a null nor a Nullable<T> value type.", nameof( t ) );
            return new JsonTypeInfo( t, _typeInfoAutoNumber++, name, startTokenType, previousNames );
        }

        /// <summary>
        /// Simple helper that calls <see cref="CreateTypeInfo"/> and <see cref="AllowTypeInfo(JsonTypeInfo)"/>.
        /// The Json type info is created and allowed but its <see cref="JsonTypeInfo.Configure(CodeWriter, CodeReader)"/> must still be called.
        /// </summary>
        /// <param name="t">The type to allow..</param>
        /// <param name="name">The serialized name.</param>
        /// <param name="previousNames">Optional list of previous names (act as type aliases).</param>
        /// <returns>The allowed type info that must still be configured.</returns>
        public JsonTypeInfo AllowTypeInfo( Type t, string name, StartTokenType startTokenType, IReadOnlyList<string>? previousNames = null )
        {
            return AllowTypeInfo( CreateTypeInfo( t, name, startTokenType, previousNames ) );
        }

        /// <summary>
        /// Registers the <see cref="IJsonCodeGenHandler.Type"/>, the <see cref="IJsonCodeGenHandler.JsonName"/> and all <see cref="IJsonCodeGenHandler.PreviousJsonNames"/>
        /// of the <see cref="JsonTypeInfo.NonNullHandler"/> and if the type is a value type, its Nullable&lt;Type&gt; is mapped to
        /// the <see cref="JsonTypeInfo.NullHandler"/>.
        /// <para>
        /// None of these keys must already exit otherwise a <see cref="ArgumentException"/> will be thrown.
        /// </para>
        /// </summary>
        /// <param name="i">The TypeInfo to register.</param>
        /// <returns>The registered TypeInfo.</returns>
        public JsonTypeInfo AllowTypeInfo( JsonTypeInfo i )
        {
            if( _finalizedCall.HasValue ) throw new InvalidOperationException( nameof( IsFinalized ) );
            Debug.Assert( i.TypeSpecOrder == 0.0f );
            // For Value type, we register the Nullable<T> type.
            if( i.Type.IsValueType )
            {
                Register( _map, _monitor, i.NonNullHandler );
                Register( _map, _monitor, i.NullHandler );

                if( _typeInfoRefTypeStartIdx == 0 )
                {
                    _typeInfos.Add( i );
                }
                else
                {
                    _typeInfos.Insert( _typeInfoRefTypeStartIdx++, i );
                }
            }
            else
            {
                Register( _map, _monitor, i.NullHandler );
                if( _typeInfoRefTypeStartIdx == 0 )
                {
                    // Regular registration has not started yet (InitializeMap is running).
                    _typeInfos.Add( i );
                }
                else
                {
                    if( i.Type.IsSealed || typeof( IPoco ).IsAssignableFrom( i.Type ) )
                    {
                        // Poco and sealed types are like value types: no specialization can exist.
                        _typeInfos.Insert( _typeInfoRefTypeStartIdx++, i );
                    }
                    else
                    {
                        // Finds the first type that can be assigned to the new one:
                        // the new one must appear before it.
                        // At the same time, update the TypeSpecOrder and update the specialization lists.
                        int idx;
                        for( idx = _typeInfoRefTypeStartIdx; idx < _typeInfos.Count; ++idx )
                        {
                            var atIdx = _typeInfos[idx];
                            Debug.Assert( atIdx.TypeSpecOrder > 0.0f, "Its TypeSpecOrder has been updated." );
                            if( atIdx.Type.IsAssignableFrom( i.Type ) )
                            {
                                // We found the position in the list: we can
                                // update the TypeSpecOrder property.
                                i.TypeSpecOrder = (atIdx.TypeSpecOrder + _typeInfos[idx - 1].TypeSpecOrder) / 2;
                                atIdx.AddSpecialization( i );
                                for( int idx2 = idx+1; idx2 < _typeInfos.Count; ++idx2 )
                                {
                                    // Unfortunately we must continue the work on the rest of the
                                    // list to be sure to capture all the specializations.
                                    // This seems inefficient, but I failed to find a better way without
                                    // yet another type tree model and the fact is that :
                                    //  - Since the JsonTypes are purely opt-in, crawling the base types is not an option
                                    //    (we don't know if they need to be registered: this would imply to manage a kind of waiting list).
                                    //  - Only "external" non-poco classes are concerned, there should not be a lot of them.
                                    atIdx = _typeInfos[idx2];
                                    if( atIdx.Type.IsAssignableFrom( i.Type ) )
                                    {
                                        atIdx.AddSpecialization( i );
                                    }
                                    else if( i.Type.IsAssignableFrom( atIdx.Type ) )
                                    {
                                        i.AddSpecialization( atIdx );
                                    }
                                }
                                break;
                            }
                            else if( i.Type.IsAssignableFrom( atIdx.Type ) )
                            {
                                i.AddSpecialization( atIdx );
                            }
                        }
                        // If the TypeSpecOrder has not been updated (we are at the end of the list),
                        // set it to 1.0 after the last one.
                        Debug.Assert( (i.TypeSpecOrder == 0) == (idx == _typeInfos.Count) );
                        if( i.TypeSpecOrder == 0 )
                        {
                            i.TypeSpecOrder = _typeInfos[^1].TypeSpecOrder + 1.0f;
                        }
                        _typeInfos.Insert( idx, i );
                    }
                }
            }
            return i;

            static void Register( Dictionary<object, IJsonCodeGenHandler> _map, IActivityMonitor monitor, IJsonCodeGenHandler h )
            {
                _map.Add( h.Type, h );
                _map.Add( h.JsonName, h );
                foreach( var p in h.PreviousJsonNames )
                {
                    if( !_map.TryAdd( p, h ) )
                    {
                        var exist = _map[p];
                        monitor.Warn( $"Previous name '{p}' for '{h.TypeInfo}' is already mapped to '{exist.TypeInfo}'. It is ignored." );
                    }
                }
            }
        }

        /// <summary>
        /// Raised when the <see cref="TypeInfoRequiredEventArg.RequiredType"/> should be allowed by registering a
        /// <see cref="JsonTypeInfo"/> for it.
        /// Note that the <see cref="JsonTypeInfo.CodeReader"/> and <see cref="JsonTypeInfo.CodeWriter"/> may be
        /// configured later: <see cref="TypeInfoConfigurationRequired"/> will be raised later whenever one of them
        /// is null.
        /// </summary>
        public event EventHandler<TypeInfoRequiredEventArg>? TypeInfoRequired;

        /// <summary>
        /// Raised when the <see cref="TypeInfoConfigurationRequiredEventArg.TypeToConfigure"/> has a
        /// null <see cref="JsonTypeInfo.CodeReader"/> or <see cref="JsonTypeInfo.CodeWriter"/>.
        /// </summary>
        public event EventHandler<TypeInfoConfigurationRequiredEventArg>? TypeInfoConfigurationRequired;

        /// <summary>
        /// Raised when the <see cref=""/> should be allowed by registering a
        /// <see cref="JsonTypeInfo"/> for it.
        /// Note that the <see cref="JsonTypeInfo.CodeReader"/> and <see cref="JsonTypeInfo.CodeWriter"/> may be
        /// configured later: <see cref="TypeInfoConfigurationRequired"/> will be raised later whenever one of them
        /// is null.
        /// </summary>
        public event EventHandler<EventMonitoredArgs>? JsonTypeFinalized;

        /// <summary>
        /// Gets a handler for a type: this is typically called from code that are creating a <see cref="CodeReader"/>/<see cref="CodeWriter"/>
        /// and needs to read/write <paramref name="t"/> instances.
        /// <para>
        /// Supported automatic types are <see cref="Enum"/>, arrays of any other allowed type, <see cref="List{T}"/>, <see cref="IList{T}"/>, <see cref="ISet{T}"/>,
        /// <see cref="HashSet{T}"/>, <see cref="Dictionary{TKey, TValue}"/> and <see cref="IDictionary{TKey, TValue}"/> where the generic parameter types must themselves
        /// be eventually allowed (see <see cref="IsAllowedType(Type)"/>).
        /// </para>
        /// </summary>
        /// <param name="t">The type to allow.</param>
        /// <param name="nullableHandler">When specified, ignores the actual type's nullability and returns the corresponding handler.</param>
        /// <returns>The handler to use. Null on error.</returns>
        public IJsonCodeGenHandler? GetHandler( Type t, bool? nullableHandler = null )
        {
            if( !_map.TryGetValue( t, out var handler ) )
            {
                JsonTypeInfo? info = null;
                if( t.IsValueType )
                {
                    bool isNullable = LiftNullableValueType( ref t );
                    if( t.IsEnum )
                    {
                        info = TryRegisterInfoForEnum( t );
                    }
                    else if( t.IsGenericType )
                    {
                        var tGen = t.GetGenericTypeDefinition();
                        if( tGen.Namespace == "System" && tGen.Name.StartsWith( "ValueTuple`", StringComparison.Ordinal ) )
                        {
                            info = TryRegisterInfoForValueTuple( t, t.GetGenericArguments() );
                        }
                    }
                    if( info != null )
                    {
                        handler = isNullable ? info.NullHandler : info.NonNullHandler;
                    }
                }
                else if( t.IsGenericType )
                {
                    IFunctionScope? fWrite = null;
                    IFunctionScope? fRead = null;

                    Type? tInterface = null;
                    Type genType = t.GetGenericTypeDefinition();
                    Type[] genArgs = t.GetGenericArguments();
                    bool isList = genType == typeof( IList<> ) || genType == typeof( List<> );
                    bool isSet = !isList && (genType == typeof( ISet<> ) || genType == typeof( HashSet<> ));
                    if( isList || isSet )
                    {
                        if( t.IsInterface )
                        {
                            tInterface = t;
                            t = (isList ? typeof( List<> ) : typeof( HashSet<> )).MakeGenericType( genArgs[0] );
                        }
                        else
                        {
                            tInterface = (isList ? typeof( IList<> ) : typeof( ISet<> )).MakeGenericType( genArgs[0] );
                        }
                        (fWrite, fRead, info) = CreateListOrSetFunctions( t, isList );
                    }
                    else if( genType == typeof( IDictionary<,> ) || genType == typeof( Dictionary<,> ) )
                    {
                        Type tKey = genArgs[0];
                        Type tValue = genArgs[1];
                        if( t.IsInterface )
                        {
                            tInterface = t;
                            t = typeof( Dictionary<,> ).MakeGenericType( tKey, tValue );
                        }
                        else
                        {
                            tInterface = typeof( IDictionary<,> ).MakeGenericType( tKey, tValue );
                        }
                        if( tKey == typeof( string ) )
                        {
                            (fWrite, fRead, info) = CreateStringMapFunctions( t, tValue );
                        }
                        else
                        {
                            (fWrite, fRead, info) = CreateMapFunctions( t, tKey, tValue );
                        }

                    }
                    if( info != null )
                    {
                        Debug.Assert( fRead != null && fWrite != null && tInterface != null );
                        handler = ConfigureAndAddTypeInfoForListSetAndMap( info, fWrite, fRead, tInterface );
                    }
                }
                else if( t.IsArray )
                {
                    // To read an array T[] we use an intermediate List<T>.
                    Type tItem = t.GetElementType()!;
                    Type tList = typeof( List<> ).MakeGenericType( tItem );
                    if( GetHandler( tList ) == null ) return null;

                    // The List<T> is now handled: generates the array.
                    IFunctionScope? fWrite = null;
                    IFunctionScope? fRead = null;
                    (fWrite, fRead, info) = CreateArrayFunctions( t, tItem );
                    if( info != null )
                    {
                        info.Configure(
                                  ( ICodeWriter write, string variableName ) =>
                                  {
                                      write.Append( "PocoDirectory_CK." ).Append( fWrite.Definition.MethodName.Name ).Append( "( w, " ).Append( variableName ).Append( ", options );" );
                                  },
                                  ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                                  {
                                      read.Append( "PocoDirectory_CK." ).Append( fRead.Definition.MethodName.Name ).Append( "( ref r, out " ).Append( variableName ).Append( ", options );" );
                                  } );
                        handler = AllowTypeInfo( info ).NullHandler;
                    }
                }
            }
            if( handler == null )
            {
                int idx = _reentrancy.IndexOf( already => already == t );
                if( idx >= 0 )
                {
                    _monitor.Error( $"Cycle detected in type registering for Json serialization: '{_reentrancy.Skip( idx ).Append( t ).Select( r => r.Name ).Concatenate( "' -> '" ) }'." );
                    return null;
                }
                _reentrancy.Push( t );
                using( _monitor.OpenTrace( $"Raising JsonTypeInfoRequired for '{t.FullName}'." ) )
                {
                    try
                    {
                        TypeInfoRequired?.Invoke( this, new TypeInfoRequiredEventArg( _monitor, this, t ) );
                    }
                    catch( Exception ex )
                    {
                        _monitor.Error( ex );
                    }
                }
                _reentrancy.Pop();
                if( !_map.TryGetValue( t, out handler ) || handler == null )
                {
                    _monitor.Error( $"Unable to handle Json serialization for type '{t}'." );
                    return null;
                }
            }
            // Honor the optional null/not null handler (override the default handler type).
            if( nullableHandler != null )
            {
                handler = nullableHandler.Value ? handler.ToNullHandler() : handler.ToNonNullHandler();
            }
            return handler;
        }

        /// <summary>
        /// Adds a type alias mapping to an already existing concrete reference type.
        /// No <see cref="JsonTypeInfo"/> is created for them, just a mapping to a couple (nullable and not nullable)
        /// of <see cref="IJsonCodeGenHandler"/> with a non null <see cref="IJsonCodeGenHandler.IsTypeMapping"/>.
        /// </summary>
        /// <param name="type">The type to map.</param>
        /// <param name="target">The mapped type.</param>
        public void AllowTypeAlias( Type type, JsonTypeInfo target )
        {
            if( type == null ) throw new ArgumentNullException( nameof( type ) );
            if( !type.IsClass && !type.IsInterface ) throw new ArgumentException( "Must be a class or an interface.", nameof( type ) );
            if( target == null ) throw new ArgumentNullException( nameof( target ) );
            _map.Add( type, new JsonTypeInfo.HandlerForTypeMapping( target.NullHandler, type ) );
        }

        /// <summary>
        /// Allows an interface with potentially multiple implementations: it is handled as an
        /// 'object'. Type information will be written.
        /// </summary>
        /// <param name="type">The untyped, abstract, type to register.</param>
        public void AllowInterfaceToUntyped( Type type )
        {
            if( type == null ) throw new ArgumentNullException( nameof( type ) );
            if( !type.IsInterface ) throw new ArgumentException( "Must be a an interface.", nameof( type ) );
            _map.Add( type, new JsonTypeInfo.HandlerForReferenceType( JsonTypeInfo.ObjectType ) );
        }

        static bool LiftNullableValueType( ref Type t )
        {
            Type? tN = Nullable.GetUnderlyingType( t );
            if( tN != null )
            {
                t = tN;
                return true;
            }
            return false;
        }

        readonly struct C : IComparable<JsonTypeInfo>
        {
            readonly JsonTypeInfo _t;
            public C( JsonTypeInfo t ) => _t = t;
            public int CompareTo( [AllowNull] JsonTypeInfo other ) => _t.TypeSpecOrder.CompareTo( other!.TypeSpecOrder );
        }

        internal static void InsertAtTypeSpecOrder( List<JsonTypeInfo> list, JsonTypeInfo i )
        {
            // Waiting for net5.
            // https://source.dot.net/#System.Private.CoreLib/CollectionsMarshal.cs
#if NET5
            Update!
#endif
            int idx = list.ToArray().AsSpan().BinarySearch( new C( i ) );
            Debug.Assert( idx < 0, "The item must not exist already in the list." );
            list.Insert( ~idx, i );
        }

        /// <summary>
        /// Generates the case pattern match on the types that must be written.
        /// The handlers must be non empty must be sorted according to the <see cref="JsonTypeInfo.TypeSpecOrder"/> and
        /// no handler with a <see cref="IJsonCodeGenHandler.TypeMapping"/> must appear otherwise an <see cref="ArgumentException"/>
        /// is thrown.
        /// </summary>
        /// <param name="write">The target write part.</param>
        /// <param name="handlers">A list of sorted handlers.</param>
        public void GenerateWriteSwitchCases( ICodeWriter write, IEnumerable<IJsonCodeGenHandler> handlers )
        {
            if( write == null ) throw new ArgumentNullException( nameof(write) );
            if( handlers == null ) throw new ArgumentNullException( nameof(handlers) );
            float order = 0.0f;
            foreach( var h in handlers )
            {
                if( h.TypeMapping != null )
                {
                    throw new ArgumentException( $"Handler for {h.Type.ToCSharpName()} is a mapping (to {h.TypeInfo.Type.ToCSharpName()}). Only actual handlers must be written.", nameof( handlers ) );
                }
                if( h.TypeInfo.TypeSpecOrder <= order && order != 0.0f )
                {
                    throw new ArgumentException( $"Handler for {h.Type.ToCSharpName()} is not correctly ordered. Handlers: {handlers.Select( h => $"{h.Type.ToCSharpName()} ({h.TypeInfo.TypeSpecOrder})" ).Concatenate()}", nameof( handlers ) );
                }
                order = h.TypeInfo.TypeSpecOrder;
                // Always consider the non null hander: a null value has already been written.
                var hN = h.ToNonNullHandler();
                write.Append( "case " ).AppendCSharpName( hN.Type, useValueTupleParentheses: false ).Append( " v: " );
                hN.GenerateWrite( write, "v", true );
                write.NewLine().Append( "break;" ).NewLine();
            }
        }

    }
}
