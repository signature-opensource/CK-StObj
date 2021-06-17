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
        readonly List<JsonTypeInfo> _typeInfos;
        int _typeInfoAutoNumber;
        // The index in the _typeInfos list where reference types that must be sorted
        // from "less IsAssignableFrom" (specialization) to "most IsAssignableFrom" (generalization)
        // so that switch case entries are correctly ordered.
        // Before this mark, there are the Value Types and the Untyped (object): InitializeMap
        // starts to count.
        int _typeInfoRefTypeStartIdx;

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
        /// The type is allowed but its <see cref="JsonTypeInfo.Configure(CodeWriter, CodeReader)"/> must still be called.
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
        /// Registers the <see cref="JsonTypeInfo.Type"/>, the <see cref="JsonTypeInfo.Name"/> and all <see cref="JsonTypeInfo.PreviousNames"/>
        /// onto the <see cref="JsonTypeInfo.NonNullHandler"/> and if the type is a value type, its Nullable&lt;Type&gt; is mapped to
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
            // For Value type, we register the Nullable<T> type.
            // Registering the nullable "name?" for everybody is useless: it will be injected in the _typeReaders map.
            if( i.Type.IsValueType )
            {
                _map.Add( i.Type, i.NonNullHandler );
                _map.Add( i.Name, i.NonNullHandler );
                foreach( var p in i.PreviousNames )
                {
                    _map.Add( p, i.NonNullHandler );
                }
                _map.Add( i.NullHandler.Type, i.NullHandler );
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
                _map.Add( i.Type, i.NullHandler );
                _map.Add( i.Name, i.NullHandler );
                foreach( var p in i.PreviousNames )
                {
                    _map.Add( p, i.NullHandler );
                }
                if( _typeInfoRefTypeStartIdx == 0 )
                {
                    _typeInfos.Add( i );
                }
                else
                {
                    if( i.Type.IsSealed )
                    {
                        _typeInfos.Insert( _typeInfoRefTypeStartIdx++, i );
                    }
                    else
                    {
                        // Finds the first type that can be assigned to the new one:
                        // the new one must appear before it.
                        // And since we found a type that is ambiguous, we can conclude
                        // that it's IsFinal flag is false.
                        int idx;
                        for( idx = _typeInfoRefTypeStartIdx; idx < _typeInfos.Count; ++idx )
                        {
                            var atIdx = _typeInfos[idx];
                            if( atIdx.Type.IsAssignableFrom( i.Type ) )
                            {
                                atIdx.IsFinal = false;
                                break;
                            }
                        }
                        _typeInfos.Insert( idx, i );
                    }
                }
            }
            return i;
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
                    IFunctionScope? fWrite = null;
                    IFunctionScope? fRead = null;
                    (fWrite, fRead, info) = CreateArrayFunctions( t );
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
            if( handler != null )
            {
                // Honor the optional null/not null handler (override the default handler type).
                if( nullableHandler != null )
                {
                    handler = nullableHandler.Value ? handler.ToNullHandler() : handler.ToNonNullHandler();
                }
            }
            return handler;
        }

        /// <summary>
        /// Adds a type alias mapping to a handler (typically to a concrete type).
        /// </summary>
        /// <param name="t">The type to map.</param>
        /// <param name="handler">The handler to use.</param>
        public void AddTypeHandlerAlias( Type t, IJsonCodeGenHandler handler )
        {
            _map.Add( t, handler.CreateAbstract( t ) );
        }

        /// <summary>
        /// Allows an untyped type: it is handled as an 'object', the type name of the
        /// concrete object will be the first item of a 2-cells array, the second being the object's value
        /// (Type information is also written when <see cref="IJsonCodeGenHandler.IsTypeMapping"/> is true
        /// or <see cref="JsonTypeInfo.IsFinal"/> is false).
        /// </summary>
        /// <param name="t">The untyped, abstract, type to register.</param>
        /// <param name="nullable">Whether this is the nullable or non-nullable type that must be registered.</param>
        public void AddUntypedHandler( Type t, bool nullable = true )
        {
            _map.Add( t, nullable ? JsonTypeInfo.Untyped.NullHandler.CreateAbstract( t ) : JsonTypeInfo.Untyped.NonNullHandler.CreateAbstract( t ) );
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

    }
}
