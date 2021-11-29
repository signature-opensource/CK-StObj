using CK.CodeGen;
using CK.Core;
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
    /// All IPoco and the transitive closure of their properties are automatically
    /// handled (by the Engine's PocoJsonSerializerImpl).
    /// <para>
    /// This service supports automatic code generation for enums or collections types (as long as they are
    /// allowed thanks to <see cref="AllowType(NullableTypeTree)"/>).
    /// Other types must be registered with their <see cref="CodeWriter"/> and <see cref="CodeReader"/> generators
    /// by providing and configuring a <see cref="JsonTypeInfo"/>.
    /// </para>
    /// This service is instantiated and registered by the Engine's PocoJsonSerializerImpl that is
    /// activated by the CK.Poco.Json package (thanks to the PocoJsonSerializer static class).
    /// </para>
    /// </summary>
    public partial class JsonSerializationCodeGen
    {
        readonly IActivityMonitor _monitor;
        readonly ITypeScope _pocoDirectory;
        readonly Stack<NullableTypeTree> _reentrancy;
        // Used to defer calls to GenerateRead/Write.
        readonly List<Action<IActivityMonitor>> _finalReadWrite;

        /// <summary>
        /// Maps Type and Names (current and previous) to its handler.
        /// </summary>
        readonly Dictionary<object, JsonCodeGenHandler> _map;
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
        readonly List<ECMAScriptStandardReader> _standardReaders;
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
            _map = new Dictionary<object, JsonCodeGenHandler>();
            _typeInfos = new List<JsonTypeInfo>();
            _reentrancy = new Stack<NullableTypeTree>();
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
        /// Gets the types or names mapping to the <see cref="JsonCodeGenHandler"/> to use (keys are either the Type
        /// object or the serialized type name or appear in the <see cref="ExternalNameAttribute.PreviousNames"/>).
        /// </summary>
        public IReadOnlyDictionary<object, JsonCodeGenHandler> HandlerMap => _map;

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
        /// This should be called only once per type that must be <see cref="NullableTypeTree.IsNormalNull"/>.
        /// To create a <see cref="NullableTypeTree"/> from a type, use the <see cref="NullabilityTypeExtensions.GetNullabilityKind(Type)"/>
        /// extension method.
        /// </summary>
        /// <param name="normalType">The type.</param>
        /// <param name="name">The non nullable serialized type name.</param>
        /// <param name="previousNames">Optional non nullable previous names.</param>
        /// <returns>A new type info.</returns>
        public JsonTypeInfo? CreateTypeInfo( NullableTypeTree normalType, string name, IReadOnlyList<string>? previousNames = null )
        {
            if( !normalType.IsNormalNull ) throw new ArgumentException( "Must be a 'normalized null' type.", nameof( normalType ) );
            if( name == null || name.Length == 0 || name[^1] == '?' ) throw new ArgumentException( "Must be a non nullable serialized type name.", nameof( name ) );
            if( normalType.Type == typeof(void) ) throw new ArgumentException( "The typeof(void) type is invalid.", nameof( name ) );

            JsonCodeGenHandler? writeHandler = null;
            if( normalType.RawSubTypes.Count > 0 )
            {
                var oblivious = normalType.Type.GetNullableTypeTree();
                if( oblivious != normalType )
                {
                    if( oblivious.Kind.IsReferenceType() ) oblivious = oblivious.ToAbnormalNull();
                    writeHandler = GetHandler( oblivious );
                    if( writeHandler == null )
                    {
                        _monitor.Error( $"Unable to obtain the GenericWriteHandler for oblivious '{oblivious}' for type '{normalType}'." );
                        return null;
                    }
                }
            }
            return new JsonTypeInfo( normalType, _typeInfoAutoNumber++, name, previousNames, writeHandler );
        }

        /// <summary>
        /// Simple helper that calls <see cref="NullabilityTypeExtensions.GetNullableTypeTree(Type, INullableTypeTreeBuilder?)"/>,
        /// <see cref="CreateTypeInfo"/> and <see cref="AllowTypeInfo(JsonTypeInfo)"/>.
        /// The Json type info is created and allowed but its <see cref="JsonTypeInfo.Configure(CodeWriter, CodeReader)"/> must still be called.
        /// </summary>
        /// <param name="t">The type to allow..</param>
        /// <param name="name">The serialized name.</param>
        /// <param name="previousNames">Optional list of previous names (act as type aliases).</param>
        /// <returns>The allowed type info that must still be configured or null if it cannot be created.</returns>
        public JsonTypeInfo? AllowTypeInfo( Type t, string name, IReadOnlyList<string>? previousNames = null ) => AllowTypeInfo( t.GetNullableTypeTree(), name, previousNames );

        /// <summary>
        /// Simple helper that calls <see cref="CreateTypeInfo"/> and <see cref="AllowTypeInfo(JsonTypeInfo)"/>.
        /// The Json type info is created and allowed but its <see cref="JsonTypeInfo.Configure(CodeWriter, CodeReader)"/> must still be called.
        /// </summary>
        /// <param name="t">The type to allow..</param>
        /// <param name="name">The serialized name.</param>
        /// <param name="previousNames">Optional list of previous names (act as type aliases).</param>
        /// <returns>The allowed type info that must still be configured or null if it cannot be created.</returns>
        public JsonTypeInfo? AllowTypeInfo( NullableTypeTree t, string name, IReadOnlyList<string>? previousNames = null )
        {
            var info = CreateTypeInfo( t, name, previousNames );
            return info == null ? null : AllowTypeInfo( info );
        }

        /// <summary>
        /// Registers the <see cref="JsonCodeGenHandler.Type"/>, the <see cref="JsonCodeGenHandler.JsonName"/> and all <see cref="JsonCodeGenHandler.PreviousJsonNames"/>
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
            // Since JsonTypeInfo.ObjectType is static, multiples runs (unit tests) register it multiple times. 
            if( i.TypeSpecOrder >= 0.0f && i != JsonTypeInfo.ObjectType )
            {
                throw new InvalidOperationException( $"JsonTypeInfo '{i.GenCSharpName}' is already registered." );
            }
            i.TypeSpecOrder = 0.0f;
            _map.Add( i.NonNullHandler.Type, i.NonNullHandler );
            _map.Add( i.NullHandler.Type, i.NullHandler );
            if( i.Type.Type.IsValueType )
            {
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
                if( _typeInfoRefTypeStartIdx == 0 )
                {
                    // Regular registration has not started yet (InitializeMap is running).
                    _typeInfos.Add( i );
                }
                else
                {
                    if( i.Type.Type.IsSealed || typeof( IPoco ).IsAssignableFrom( i.Type.Type ) )
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
                            if( atIdx.MostAbstractType.IsAssignableFrom( i.MostAbstractType ) )
                            {
                                // We found the position in the list: we can
                                // update the TypeSpecOrder property.
                                i.TypeSpecOrder = (atIdx.TypeSpecOrder + _typeInfos[idx - 1].TypeSpecOrder) / 2;
                                atIdx.AddSpecialization( i );
                                for( int idx2 = idx + 1; idx2 < _typeInfos.Count; ++idx2 )
                                {
                                    // Unfortunately we must continue the work on the rest of the
                                    // list to be sure to capture all the specializations.
                                    // This seems inefficient, but I failed to find a better way without
                                    // yet another type tree model and the fact is that :
                                    //  - Since the JsonTypes are purely opt-in, crawling the base types is not an option
                                    //    (we don't know if they need to be registered: this would imply to manage a kind of waiting list).
                                    //  - Only "external" non-poco classes are concerned, there should not be a lot of them.
                                    atIdx = _typeInfos[idx2];
                                    if( atIdx.MostAbstractType.IsAssignableFrom( i.MostAbstractType ) )
                                    {
                                        atIdx.AddSpecialization( i );
                                    }
                                    else if( i.MostAbstractType.IsAssignableFrom( atIdx.MostAbstractType ) )
                                    {
                                        i.AddSpecialization( atIdx );
                                    }
                                }
                                break;
                            }
                            else if( i.MostAbstractType.IsAssignableFrom( atIdx.MostAbstractType ) )
                            {
                                i.AddSpecialization( atIdx );
                            }
                        }
                        // If the TypeSpecOrder has not been updated (we are at the end of the list),
                        // set it to +1.0 after the last one.
                        Debug.Assert( (i.TypeSpecOrder == 0.0f) == (idx == _typeInfos.Count) );
                        if( i.TypeSpecOrder == 0 )
                        {
                            i.TypeSpecOrder = _typeInfos[^1].TypeSpecOrder + 1.0f;
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
        /// Simple helper that calls <see cref="GetHandler(NullableTypeTree)"/>: either the type is
        /// already mapped or, for enums, lists, dictionary and sets, the handlers are generated. 
        /// </summary>
        /// <para>
        /// The <c>typeof(void)</c> is invalid (an <see cref="ArgumentException"/> is thrown).
        /// </para>
        /// <param name="t">The <see cref="NullableTypeTree"/> to support.</param>
        /// <returns>True on success, false on error.</returns>
        public bool AllowType( NullableTypeTree t ) => GetHandler( t ) != null;

        /// <summary>
        /// Gets a handler for a type: this is typically called from code that are creating a <see cref="CodeReader"/>/<see cref="CodeWriter"/>
        /// and needs to read/write <paramref name="t"/> instances.
        /// <para>
        /// Supported automatic types are <see cref="Enum"/>, arrays of any other allowed type, <see cref="List{T}"/>, <see cref="IList{T}"/>, <see cref="ISet{T}"/>,
        /// <see cref="HashSet{T}"/>, <see cref="Dictionary{TKey, TValue}"/> and <see cref="IDictionary{TKey, TValue}"/> where the generic parameter types must themselves
        /// be eventually allowed (see <see cref="IsAllowedType(Type)"/>).
        /// </para>
        /// <para>
        /// The <c>typeof(void)</c> is invalid (an <see cref="ArgumentException"/> is thrown).
        /// </para>
        /// </summary>
        /// <param name="t">The type to allow.</param>
        /// <returns>The handler to use. Null on error.</returns>
        public JsonCodeGenHandler? GetHandler( NullableTypeTree t )
        {
            if( !_map.TryGetValue( t, out var handler ) )
            {
                if( t.Type == typeof(void) )
                {
                    throw new ArgumentException( "Type typeof(void) is invalid.", nameof( t ) );
                }
                JsonTypeInfo? info = null;
                if( (t.Kind & NullabilityTypeKind.IsValueType) != 0 )
                {
                    if( t.Type.IsEnum )
                    {
                        info = TryRegisterInfoForEnum( t );
                    }
                    else if( t.Kind.IsTupleType() )
                    {
                        info = TryRegisterInfoForValueTuple( t, t.IsLongValueTuple ? t.SubTypes.ToList() : t.RawSubTypes );
                    }
                }
                else if( t.Type.IsGenericType )
                {
                    IFunctionScope? fWrite = null;
                    IFunctionScope? fRead = null;

                    Type classCollType = t.Type;
                    NullableTypeTree? tInterface = null;
                    Type genType = classCollType.GetGenericTypeDefinition();
                    Type[] genArgs = classCollType.GetGenericArguments();
                    bool isList = genType == typeof( IList<> ) || genType == typeof( List<> );
                    bool isSet = !isList && (genType == typeof( ISet<> ) || genType == typeof( HashSet<> ));
                    if( isList || isSet )
                    {
                        if( classCollType.IsInterface )
                        {
                            tInterface = t;
                            classCollType = (isList ? typeof( List<> ) : typeof( HashSet<> )).MakeGenericType( genArgs[0] );
                            t = t.With( classCollType );
                        }
                        else
                        {
                            tInterface = t.With( (isList ? typeof( IList<> ) : typeof( ISet<> )).MakeGenericType( genArgs[0] ) );
                        }
                        (fWrite, fRead, info) = CreateListOrSetFunctions( t, isList );
                    }
                    else if( genType == typeof( IDictionary<,> ) || genType == typeof( Dictionary<,> ) )
                    {
                        Type tKey = genArgs[0];
                        Type tValue = genArgs[1];
                        if( t.Type.IsInterface )
                        {
                            tInterface = t;
                            t = t.With( typeof( Dictionary<,> ).MakeGenericType( tKey, tValue ) );
                        }
                        else
                        {
                            tInterface = t.With( typeof( IDictionary<,> ).MakeGenericType( tKey, tValue ) );
                        }
                        if( tKey == typeof( string ) )
                        {
                            (fWrite, fRead, info) = CreateStringMapFunctions( t, t.RawSubTypes[1] );
                        }
                        else
                        {
                            (fWrite, fRead, info) = CreateMapFunctions( t, t.RawSubTypes[0], t.RawSubTypes[1] );
                        }

                    }
                    if( info != null )
                    {
                        Debug.Assert( fRead != null && fWrite != null && tInterface != null );
                        handler = ConfigureAndAddTypeInfoForListSetAndMap( info, fWrite, fRead, tInterface.Value );
                    }
                }
                else if( t.Type.IsArray )
                {
                    // To read an array T[] we use an intermediate List<T>.
                    NullableTypeTree tItem = t.RawSubTypes[0];
                    Debug.Assert( t.Type.GetElementType() == (tItem.Kind.IsNullableValueType()
                                                                ? typeof( Nullable<> ).MakeGenericType( tItem.Type )
                                                                : tItem.Type) );
                    var actualTypeList = typeof( List<> ).MakeGenericType( t.Type.GetElementType()! );
                    NullableTypeTree tList = t.With( actualTypeList, t.Kind | NullabilityTypeKind.IsGenericType );
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
                                      read.Append( variableName ).Append( " = PocoDirectory_CK." ).Append( fRead.Definition.MethodName.Name ).Append( "( ref r, options );" );
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
                    _monitor.Error( $"Cycle detected in type registering for Json serialization: '{_reentrancy.Skip( idx ).Append( t ).Select( r => r.ToString() ).Concatenate( "' -> '" ) }'." );
                    return null;
                }
                _reentrancy.Push( t );
                using( _monitor.OpenTrace( $"Raising JsonTypeInfoRequired for '{t}'." ) )
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
                    _monitor.Warn( $"Unable to handle Json serialization for type '{t}': relying on System.Text.Json.JsonSerializer.Serialize/Deserialize methods." );
                    var info = CreateTypeInfoWithJsonSerializer( t );
                    if( info == null ) return null;
                    handler = AllowTypeInfo( info ).NullHandler;
                }
            }
            if( handler.IsNullable != ((t.Kind & NullabilityTypeKind.IsNullable) != 0) )
            {
                if( handler.IsNullable ) handler = handler.ToNonNullHandler();
                else handler = handler.ToNullHandler();
            }
            return handler;
        }

        JsonTypeInfo? CreateTypeInfoWithJsonSerializer( NullableTypeTree t )
        {
            var info = CreateTypeInfo( t.ToNormalNull(), t.ToNonNullable().ToString( true ) );
            if( info == null ) return null;
            info.Configure( ( ICodeWriter write, string variableName ) =>
            {
                write.Append( "System.Text.Json.JsonSerializer.Serialize<" )
                     .AppendCSharpName( t.Type, false )
                     .Append( ">( w, " ).Append( variableName ).Append( ", (options ?? PocoJsonSerializerOptions.Default).ForJsonSerializer );" );
            },
            ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
            {
                read.Append( variableName ).Append( " = System.Text.Json.JsonSerializer.Deserialize<" )
                     .AppendCSharpName( t.Type, false )
                     .Append( ">( ref r, (options ?? PocoJsonSerializerOptions.Default).ForJsonSerializer );" ).NewLine()
                     .Append( "r.Read();" );
            } );
            return info;
        }

        /// <summary>
        /// Adds an unambiguous reference type alias mapping to an already existing concrete reference type.
        /// No <see cref="JsonTypeInfo"/> is created for them, just a mapping to a couple (nullable and not nullable)
        /// of <see cref="JsonCodeGenHandler"/> with a non null <see cref="JsonCodeGenHandler.IsTypeMapping"/>.
        /// <para>
        /// The <paramref name="alias"/> must be assignable from <paramref name="target"/>' type otherwise an <see cref="ArgumentException"/> is thrown.
        /// </para>
        /// <para>
        /// It is always the target type that is written and since it is assignable to the alias, it can be read back without downcasts.
        /// </para>
        /// </summary>
        /// <param name="alias">The reference type to map that is assignable to the target's type. Must be <see cref="NullableTypeTree.IsNormalNull"/>: and since
        /// this it is a reference type, it must be nullable.</param>
        /// <param name="target">The mapped type.</param>
        /// <param name="isMostAbstract">
        /// Optionally sets the <see cref="JsonTypeInfo.MostAbstractMapping"/>.
        /// This must be set once and only once and target's <see cref="JsonTypeInfo.IsRegistered"/> must be false.
        /// </param>
        public void AllowTypeAlias( NullableTypeTree alias, JsonTypeInfo target, bool isMostAbstract = false )
        {
            if( !alias.IsNormalNull ) throw new ArgumentException( "Must be a 'normalized null' type.", nameof( alias ) );
            if( !(alias.Type.IsClass || alias.Type.IsSealed) && !alias.Type.IsInterface ) throw new ArgumentException( $"Must be a non sealed class or an interface. '{alias}' is not.", nameof( alias ) );
            if( target == null ) throw new ArgumentNullException( nameof( target ) );
            if( !alias.Type.IsAssignableFrom( target.Type.Type ) ) throw new ArgumentException( $"Type alias '{alias}' must be assignable to '{target.GenCSharpName}'.", nameof( alias ) );
            var hNull = AllowMapping( alias, target.NullHandler );
            if( isMostAbstract )
            {
                if( target.IsRegistered )
                {
                    throw new InvalidOperationException( $"AllowTypeAlias for alias {alias} must be called before the target '{target.GenCSharpName}' is registered (via AllowTypeInfo)." );
                }
                if( target.MostAbstractMapping != null )
                {
                    throw new InvalidOperationException( $"Type alias '{alias}' cannot be defined as the MostAbstractType on '{target.GenCSharpName}' since it is already defined as '{target.MostAbstractMapping.GenCSharpName}'." );
                }
                target.MostAbstractMapping = hNull;
            }
        }

        /// <summary>
        /// Allows an interface with potentially multiple implementations: it is handled as an
        /// 'object': the <see cref="JsonTypeInfo.ObjectType"/> is used that writes (with type information) and reads
        /// back an object: a downcast is generated to read this <paramref name="type"/>.
        /// </summary>
        /// <param name="type">The interface to allow.</param>
        public void AllowInterfaceToUntyped( Type type )
        {
            if( type == null ) throw new ArgumentNullException( nameof( type ) );
            if( !type.IsInterface ) throw new ArgumentException( "Must be a an interface.", nameof( type ) );
            AllowMapping( type.GetNullableTypeTree(), JsonTypeInfo.ObjectType.NullHandler );
        }

        JsonTypeInfo.HandlerForTypeMapping AllowMapping( NullableTypeTree tree, JsonCodeGenHandler h )
        {
            var hNull = new JsonTypeInfo.HandlerForTypeMapping( h, tree );
            _map.Add( tree, hNull );
            _map.Add( tree.ToAbnormalNull(), hNull.ToNonNullHandler() );
            return hNull;
        }


        readonly struct C : IComparable<JsonTypeInfo>
        {
            readonly JsonTypeInfo _t;
            public C( JsonTypeInfo t ) => _t = t;
            public int CompareTo( [AllowNull] JsonTypeInfo other ) => _t.TypeSpecOrder.CompareTo( other!.TypeSpecOrder );
        }

        /// <summary>
        /// Inserts a <see cref="JsonTypeInfo"/> in a list based on its <see cref="JsonTypeInfo.TypeSpecOrder"/>.
        /// This must be called with a <paramref name="startIndex"/> that skips any TypeSpecOrder = 0.0f.
        /// </summary>
        /// <param name="list">The target list.</param>
        /// <param name="i">The info to insert.</param>
        /// <param name="startIndex">The starting index to consider in the list.</param>
        /// <returns>True on success, false on duplicate.</returns>
        public static bool InsertAtTypeSpecOrderUnsafe( List<JsonTypeInfo> list, JsonTypeInfo i, int startIndex )
        {
            // Waiting for net5.
            // https://source.dot.net/#System.Private.CoreLib/CollectionsMarshal.cs
#if NET5
            Update!
#endif
            int idx = list.ToArray().AsSpan().Slice( startIndex ).BinarySearch( new C( i ) );
            if( idx >= 0 )
            {
                if( i != list[idx] ) throw new CKException( $"Invalid JsonTypeInfo: found 2 different info with the same TypeSpecOrder: '{i.Type}' and '{list[idx]}' (TypeSpecOder = {i.TypeSpecOrder})." );
                return false;
            }
            list.Insert( ~idx, i );
            return true;
        }

        /// <summary>
        /// Inserts a <see cref="JsonTypeInfo"/> in a list based on its <see cref="JsonTypeInfo.TypeSpecOrder"/>.
        /// No duplicate detection is done here since that would require to analyze a range of items (for TypeSpecOrder = 0.0f).
        /// </summary>
        /// <param name="list">The target list.</param>
        /// <param name="i">The info to insert.</param>
        /// <returns>True on success, false on duplicate.</returns>
        public static bool InsertAtTypeSpecOrder( List<JsonTypeInfo> list, JsonTypeInfo info )
        {
            if( list.Count == 0 )
            {
                list.Add( info );
                return true;
            }
            int i = 0;
            for( ; i < list.Count; ++i )
            {
                var item = list[i];
                if( item.TypeSpecOrder == 0.0f )
                {
                    if( item == info ) return false;
                    continue;
                }
                if( info.TypeSpecOrder == 0.0f )
                {
                    list.Insert( i, info );
                    return true;
                }
                break;
            }
            return InsertAtTypeSpecOrderUnsafe( list, info, i );
        }

        /// <summary>
        /// Generates the case pattern match on the types that must be written.
        /// The handlers must be non empty, must be sorted according to the <see cref="JsonTypeInfo.TypeSpecOrder"/> and
        /// no handler with a <see cref="JsonCodeGenHandler.TypeMapping"/> must appear otherwise an <see cref="ArgumentException"/>
        /// is thrown.
        /// </summary>
        /// <param name="write">The target write part.</param>
        /// <param name="handlers">A list of sorted handlers.</param>
        public void GenerateWriteSwitchCases( ICodeWriter write, IEnumerable<JsonCodeGenHandler> handlers )
        {
            if( write == null ) throw new ArgumentNullException( nameof( write ) );
            if( handlers == null ) throw new ArgumentNullException( nameof( handlers ) );
            float order = 0.0f;
            foreach( var h in handlers )
            {
                if( h.TypeMapping != null )
                {
                    throw new ArgumentException( $"Handler for {h.GenCSharpName} is a mapping (to {h.TypeInfo.GenCSharpName}). Only actual handlers must be written.", nameof( handlers ) );
                }
                if( h.TypeInfo.TypeSpecOrder <= order && order != 0.0f )
                {
                    throw new ArgumentException( $"Handler for {h.GenCSharpName} is not correctly ordered. Handlers: {handlers.Select( h => $"{h.GenCSharpName} ({h.TypeInfo.TypeSpecOrder})" ).Concatenate()}", nameof( handlers ) );
                }
                order = h.TypeInfo.TypeSpecOrder;
                // Always consider the non null hander: a null value has already been written.
                var hN = h.ToNonNullHandler();
                write.Append( "case " ).Append( hN.GenCSharpName ).Append( " v: " );
                hN.GenerateWrite( write, "v", true );
                write.NewLine().Append( "break;" ).NewLine();
            }
        }

    }
}
