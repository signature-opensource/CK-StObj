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
    /// allowed thanks to <see cref="AllowType(Type, bool?)"/>).
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
        /// <summary>
        /// Maps Type and Names (current and previous) to its handler.
        /// </summary>
        readonly Dictionary<object, IJsonCodeGenHandler> _map;
        readonly List<JsonTypeInfo> _typeInfos;
        int _typeInfoCurrentCount;

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
            InitializeMap();
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
        /// Factory for <see cref="JsonTypeInfo"/>: its <see cref="JsonTypeInfo.NumberName"/> is unique.
        /// This should be called only once per type (that must not be <see cref="Nullable{T}"/>).
        /// </summary>
        /// <param name="t">The type.</param>
        /// <param name="name">The serialized type name.</param>
        /// <param name="previousNames">Optional previous names.</param>
        /// <param name="d">Optional <see cref="DirectType"/>.</param>
        /// <param name="isAbstractType">True for an abstract type (type mapping).</param>
        /// <returns>A new type info.</returns>
        public JsonTypeInfo CreateTypeInfo( Type t, string name, IReadOnlyList<string>? previousNames = null, JsonDirectType d = JsonDirectType.None, bool isAbstractType = false )
        {
            if( t.IsValueType && Nullable.GetUnderlyingType( t ) != null ) throw new ArgumentException( "Must not be a Nullable<T> value type.", nameof( t ) );
            return new JsonTypeInfo( t, _typeInfoCurrentCount++, name, previousNames, d, isAbstractType );
        }

        /// <summary>
        /// Simple helper that calls <see cref="CreateTypeInfo"/> and <see cref="AllowTypeInfo(JsonTypeInfo)"/>.
        /// The type is allowed but its <see cref="JsonTypeInfo.Configure(CodeWriter, CodeReader)"/> must still be called.
        /// </summary>
        /// <param name="t">The type to allow..</param>
        /// <param name="name">The serialized name.</param>
        /// <param name="previousNames">Optional list of previous names (act as type aliases).</param>
        /// <returns>The allowed type info that must still be configured.</returns>
        public JsonTypeInfo AllowTypeInfo( Type t, string name, IReadOnlyList<string>? previousNames = null )
        {
            return AllowTypeInfo( CreateTypeInfo( t, name, previousNames ) );
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
            // For Value type, we register the Nullable<T> type.
            // Registering the nullable "name?" is useless for everybody: FillDynamicMap will do the job.
            _map.Add( i.Type, i.NonNullHandler );
            _map.Add( i.Name, i.NonNullHandler );
            foreach( var p in i.PreviousNames )
            {
                _map.Add( p, i.NonNullHandler );
            }
            if( i.Type.IsValueType )
            {
                _map.Add( i.NullHandler.Type, i.NullHandler );
            }
            _typeInfos.Add( i );
            return i;
        }

        /// <summary>
        /// Raised when the <see cref="TypeInfoRequiredEventArg.RequiredType"/> should be allowed by registering a
        /// <see cref="JsonTypeInfo"/> for it.
        /// Note that the <see cref="JsonTypeInfo.CodeReader"/> and <see cref="JsonTypeInfo.CodeWriter"/> may be
        /// configured later: <see cref="TypeInfoConfigurationRequired"/> will be raised later whenever one of them
        /// is null.
        /// </summary>
        public EventHandler<TypeInfoRequiredEventArg>? TypeInfoRequired;

        /// <summary>
        /// Raised when the <see cref="TypeInfoConfigurationRequiredEventArg.TypeToConfigure"/> has a
        /// null <see cref="JsonTypeInfo.CodeReader"/> or <see cref="JsonTypeInfo.CodeWriter"/>.
        /// </summary>
        public EventHandler<TypeInfoConfigurationRequiredEventArg>? TypeInfoConfigurationRequired;

        /// <summary>
        /// Allows a type so that it is known to the serializer and returns the handler to use for it: this is typically called
        /// from code that are creating a <see cref="CodeReader"/>/<see cref="CodeWriter"/> functions for a type that requires the
        /// type <paramref name="t"/>.
        /// Supported automatic types are <see cref="Enum"/>, arrays of any other
        /// allowed type, <see cref="List{T}"/>, <see cref="IList{T}"/>, <see cref="ISet{T}"/>, <see cref="HashSet{T}"/>, <see cref="Dictionary{TKey, TValue}"/>
        /// and <see cref="IDictionary{TKey, TValue}"/> where the generic parameter types must themselves be eventually allowed (see <see cref="IsAllowedType(Type)"/>).
        /// </summary>
        /// <param name="t">The type to allow.</param>
        /// <param name="nullableHandler">When specified, ignores the actual type's nullability and returns the corresponding handler.</param>
        /// <returns>The handler to use. Null on error.</returns>
        public IJsonCodeGenHandler? AllowType( Type t, bool? nullableHandler = null )
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
                        if( tGen.Namespace == "System" && tGen.Name.StartsWith( "ValueTuple`" ) )
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
                                      write.Append( "PocoDirectory_CK." ).Append( fWrite.Definition.MethodName.Name ).Append( "( w, " ).Append( variableName ).Append( " );" );
                                  },
                                  ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                                  {
                                      read.Append( "PocoDirectory_CK." ).Append( fRead.Definition.MethodName.Name ).Append( "( ref r, out " ).Append( variableName ).Append( " );" );
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
                using( _monitor.OpenTrace($"Raising JsonTypeInfoRequired for '{t.FullName}'." ))
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
        /// concrete object will be the first item of a 2-cells array, the second being the object's value.
        /// </summary>
        /// <param name="t">The untyped, abstract, type to register.</param>
        /// <param name="nullable">Whether this is the nullable or non-nullable type that must be registered.</param>
        public void AddUntypedHandler( Type t, bool nullable = true )
        {
            _map.Add( t, nullable ? JsonTypeInfo.Untyped.NullHandler.CreateAbstract( t ) : JsonTypeInfo.Untyped.NonNullHandler.CreateAbstract( t ) );
        }

        void InitializeMap()
        {
            // Direct types.
            AllowTypeInfo( JsonTypeInfo.Untyped );
            AllowTypeInfo( CreateTypeInfo( typeof( string ), "string", previousNames: null, JsonDirectType.String ) );
            AllowTypeInfo( CreateTypeInfo( typeof( bool ), "bool", previousNames: null, JsonDirectType.Boolean ) );
            AllowTypeInfo( CreateTypeInfo( typeof( int ), "int", previousNames: null, JsonDirectType.Number ) );

            static void WriteString( ICodeWriter write, string variableName )
            {
                write.Append( "w.WriteStringValue( " ).Append( variableName ).Append( " );" );
            }

            static void WriteNumber( ICodeWriter write, string variableName )
            {
                write.Append( "w.WriteNumberValue( " ).Append( variableName ).Append( " );" );
            }

            AllowTypeInfo( typeof( byte[] ), "byte[]" ).Configure(
                ( ICodeWriter write, string variableName ) =>
                {
                    write.Append( "w.WriteBase64StringValue( " ).Append( variableName ).Append( " );" );
                },
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetBytesFromBase64(); r.Read();" );
                } );

            AllowTypeInfo( typeof( Guid ), "g" ).Configure( WriteString,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetGuid(); r.Read();" );
                } );

            AllowTypeInfo( typeof( Decimal ), "Decimal" ).Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetDecimal(); r.Read();" );
                } );

            AllowTypeInfo( typeof( uint ), "uint" ).Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetUInt32(); r.Read();" );
                } );

            AllowTypeInfo( typeof( double ), "double" ).Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetDouble(); r.Read();" );
                } );

            AllowTypeInfo( typeof( float ), "float" ).Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetSingle(); r.Read();" );
                } );

            AllowTypeInfo( typeof( long ), "long" ).Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetInt64(); r.Read();" );
                } );

            AllowTypeInfo( typeof( ulong ), "ulong" ).Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetUInt64(); r.Read();" );
                } );

            AllowTypeInfo( typeof( byte ), "byte" ).Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetByte(); r.Read();" );
                } );

            AllowTypeInfo( typeof( sbyte ), "sbyte" ).Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetSByte(); r.Read();" );
                } );

            AllowTypeInfo( typeof( short ), "short" ).Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetInt16(); r.Read();" );
                } );

            AllowTypeInfo( typeof( ushort ), "ushort" ).Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetUInt16(); r.Read();" );
                } );

            AllowTypeInfo( typeof( DateTime ), "DateTime" ).Configure( WriteString,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetDateTime(); r.Read();" );
                } );

            AllowTypeInfo( typeof( DateTimeOffset ), "DateTimeOffset" ).Configure( WriteString,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetDateTimeOffset(); r.Read();" );
                } );

            AllowTypeInfo( typeof( TimeSpan ), "TimeSpan" ).Configure(
                ( ICodeWriter write, string variableName ) =>
                {
                    write.Append( "w.WriteNumberValue( " ).Append( variableName ).Append( ".Ticks );" );
                },
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = TimeSpan.FromTicks( r.GetInt64() ); r.Read();" );
                } );
        }

        IJsonCodeGenHandler ConfigureAndAddTypeInfoForListSetAndMap( JsonTypeInfo info, IFunctionScope fWrite, IFunctionScope fRead, Type tInterface )
        {
            Debug.Assert( !info.Type.IsInterface && tInterface.IsInterface );
            info.Configure(
                      ( ICodeWriter write, string variableName ) =>
                      {
                          write.Append( "PocoDirectory_CK." ).Append( fWrite.Definition.MethodName.Name ).Append( "( w, " ).Append( variableName ).Append( " );" );
                      },
                      ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                      {
                          if( !assignOnly )
                          {
                              if( isNullable )
                              {
                                  read.Append( "if( " ).Append( variableName ).Append( " == null )" )
                                      .OpenBlock()
                                      .Append( variableName ).Append( " = new " ).AppendCSharpName( info.Type ).Append( "();" )
                                      .CloseBlock();
                              }
                              else
                              {
                                  read.Append( variableName ).Append( ".Clear();" ).NewLine();
                              }
                          }
                          else
                          {
                              read.Append( variableName ).Append( " = new " ).AppendCSharpName( info.Type ).Append( "();" ).NewLine();
                          }
                          read.Append( "PocoDirectory_CK." ).Append( fRead.Definition.MethodName.Name ).Append( "( ref r, " ).Append( variableName ).Append( " );" );
                      } );
            AllowTypeInfo( info );
            // The interface is directly mapped to the non null handler.
            AddTypeHandlerAlias( tInterface, info.NonNullHandler );
            return info.NullHandler;
        }

        (IFunctionScope fWrite, IFunctionScope fRead, JsonTypeInfo info) CreateMapFunctions( Type tMap, Type tKey, Type tValue )
        {
            var keyHandler = AllowType( tKey );
            if( keyHandler == null ) return default;
            var valueHandler = AllowType( tValue );
            if( valueHandler == null ) return default;

            string keyTypeName = keyHandler.Type.ToCSharpName();
            string valueTypeName = valueHandler.Type.ToCSharpName();
            var concreteTypeName = "Dictionary<" + keyTypeName + "," + valueTypeName + ">";

            string funcSuffix = keyHandler.Info.NumberName + "_" + valueHandler.Info.NumberName;
            // Trick: the reader/writer functions accepts the interface rather than the concrete type.
            var fWriteDef = FunctionDefinition.Parse( "internal static void WriteM_" + funcSuffix + "( System.Text.Json.Utf8JsonWriter w, I" + concreteTypeName + " c )" );
            var fReadDef = FunctionDefinition.Parse( "internal static void ReadM_" + funcSuffix + "( ref System.Text.Json.Utf8JsonReader r, I" + concreteTypeName + " c )" );
            IFunctionScope? fWrite = _pocoDirectory.FindFunction( fWriteDef.Key, false );
            IFunctionScope? fRead;
            if( fWrite != null )
            {
                fRead = _pocoDirectory.FindFunction( fReadDef.Key, false );
                Debug.Assert( fRead != null );
            }
            else
            {
                fWrite = _pocoDirectory.CreateFunction( fWriteDef );
                fWrite.Append( "w.WriteStartArray();" ).NewLine()
                      .Append( "foreach( var e in c )" )
                      .OpenBlock()
                      .Append( "w.WriteStartArray();" ).NewLine();

                keyHandler.GenerateWrite( fWrite, "e.Key" );
                valueHandler.GenerateWrite( fWrite, "e.Value" );

                fWrite.Append( "w.WriteEndArray();" )
                      .CloseBlock()
                      .Append( "w.WriteEndArray();" ).NewLine();

                fRead = _pocoDirectory.CreateFunction( fReadDef );
                fRead.Append( "r.Read();" ).NewLine()
                     .Append( "while( r.TokenType != System.Text.Json.JsonTokenType.EndArray)" )
                     .OpenBlock()
                     .Append( "r.Read();" ).NewLine();

                fRead.AppendCSharpName( tKey ).Append( " k;" ).NewLine();
                keyHandler.GenerateRead( fRead, "k", true );

                fRead.NewLine()
                     .AppendCSharpName( tValue ).Append( " v;" ).NewLine();
                valueHandler.GenerateRead( fRead, "v", true );

                fRead.Append( "r.Read();" ).NewLine()
                     .Append( "c.Add( k, v );" )
                     .CloseBlock()
                     .Append( "r.Read();" );

            }
            return (fWrite, fRead, CreateTypeInfo( tMap, "M(" + keyHandler.Name + "," + valueHandler.Name + ")" ));
        }

        (IFunctionScope fWrite, IFunctionScope fRead, JsonTypeInfo info) CreateStringMapFunctions( Type tMap, Type tValue )
        {
            var valueHandler = AllowType( tValue );
            if( valueHandler == null ) return default;

            string valueTypeName = valueHandler.Type.ToCSharpName();
            var concreteTypeName = "Dictionary<string," + valueTypeName + ">";
            var fWriteDef = FunctionDefinition.Parse( "internal static void WriteO_" + valueHandler.Info.NumberName + "( System.Text.Json.Utf8JsonWriter w, I" + concreteTypeName + " c )" );
            var fReadDef = FunctionDefinition.Parse( "internal static void ReadO_" + valueHandler.Info.NumberName + "( ref System.Text.Json.Utf8JsonReader r, I" + concreteTypeName + " c )" );
            IFunctionScope? fWrite = _pocoDirectory.FindFunction( fWriteDef.Key, false );
            IFunctionScope? fRead;
            if( fWrite != null )
            {
                fRead = _pocoDirectory.FindFunction( fReadDef.Key, false );
                Debug.Assert( fRead != null );
            }
            else
            {
                fWrite = _pocoDirectory.CreateFunction( fWriteDef );
                fWrite.Append( "w.WriteStartObject();" ).NewLine()
                      .Append( "foreach( var e in c )" )
                      .OpenBlock()
                      .Append( "w.WritePropertyName( e.Key );" );
                valueHandler.GenerateWrite( fWrite, "e.Value" );
                fWrite.CloseBlock()
                 .Append( "w.WriteEndObject();" ).NewLine();

                fRead = _pocoDirectory.CreateFunction( fReadDef );
                fRead.Append( "r.Read();" ).NewLine()
                    .AppendCSharpName( tValue ).Append( " v;" ).NewLine()
                    .Append( "while( r.TokenType != System.Text.Json.JsonTokenType.EndObject )" )
                    .OpenBlock()
                    .Append( "string k = r.GetString();" ).NewLine()
                    .Append( "r.Read();" ).NewLine();
                valueHandler.GenerateRead( fRead, "v", false );
                fRead.Append( "c.Add( k, v );" )
                     .CloseBlock()
                     .Append( "r.Read();" );
            }
            return (fWrite, fRead, CreateTypeInfo( tMap, "O<" + valueHandler.Name + ">" ));
        }

        (IFunctionScope fWrite, IFunctionScope fRead, JsonTypeInfo info) CreateListOrSetFunctions( Type tColl, bool isList )
        {
            Type tItem = tColl.GetGenericArguments()[0];

            if( !CreateWriteEnumerable( tItem, out IFunctionScope? fWrite, out IJsonCodeGenHandler? itemHandler, out string? itemTypeName ) ) return default;

            var fReadDef = FunctionDefinition.Parse( "internal static void ReadLOrS_" + itemHandler.Info.NumberName + "( ref System.Text.Json.Utf8JsonReader r, ICollection<" + itemTypeName + "> c )" );
            IFunctionScope? fRead = _pocoDirectory.FindFunction( fReadDef.Key, false );
            if( fRead == null )
            {
                fRead = _pocoDirectory.CreateFunction( fReadDef );
                fRead.Append( "r.Read();" ).NewLine()
                     .AppendCSharpName( tItem ).Append( " v;" ).NewLine()
                     .Append( "while( r.TokenType != System.Text.Json.JsonTokenType.EndArray )" )
                     .OpenBlock();
                itemHandler.GenerateRead( fRead, "v", false );
                fRead.Append( "c.Add( v );" )
                     .CloseBlock()
                     .Append( "r.Read();" );
            }

            return (fWrite, fRead, CreateTypeInfo( tColl, (isList ? "L(" : "S(") + itemHandler.Name + ")" ));
        }

        (IFunctionScope fWrite, IFunctionScope fRead, JsonTypeInfo info) CreateArrayFunctions( Type tArray )
        {
            Debug.Assert( tArray.IsArray );
            Type tItem = tArray.GetElementType()!;

            if( !CreateWriteEnumerable( tItem, out IFunctionScope? fWrite, out IJsonCodeGenHandler? itemHandler, out string? itemTypeName ) ) return default;

            var fReadDef = FunctionDefinition.Parse( "internal static void ReadArray_" + itemHandler.Info.NumberName + "( ref System.Text.Json.Utf8JsonReader r, out " + itemTypeName + "[] a )" );
            IFunctionScope? fRead = _pocoDirectory.FindFunction( fReadDef.Key, false );
            if( fRead == null )
            {
                fRead = _pocoDirectory.CreateFunction( fReadDef );
                fRead.OpenBlock()
                     .Append( "var c = new List<" + itemTypeName + ">();" ).NewLine()
                     .Append( "ReadLOrS_" + itemHandler.Info.NumberName + "( ref r, c );" ).NewLine()
                     .Append( "a = c.ToArray();" ).NewLine()
                     .CloseBlock();
            }
            return (fWrite, fRead, CreateTypeInfo( tArray, itemHandler.Name + "[]" ));
        }

        bool CreateWriteEnumerable( Type tItem,
                                    [NotNullWhen( true )] out IFunctionScope? fWrite,
                                    [NotNullWhen( true )] out IJsonCodeGenHandler? itemHandler,
                                    [NotNullWhen( true )] out string? itemTypeName )
        {
            fWrite = null;
            itemTypeName = null;
            itemHandler = AllowType( tItem );
            if( itemHandler != null )
            {
                itemTypeName = itemHandler.Type.ToCSharpName();
                var fWriteDef = FunctionDefinition.Parse( "internal static void WriteE_" + itemHandler.Info.NumberName + "( System.Text.Json.Utf8JsonWriter w, IEnumerable<" + itemTypeName + "> c )" );
                fWrite = _pocoDirectory.FindFunction( fWriteDef.Key, false );
                if( fWrite == null )
                {
                    fWrite = _pocoDirectory.CreateFunction( fWriteDef );
                    fWrite.Append( "w.WriteStartArray();" ).NewLine()
                    .Append( "foreach( var e in c )" )
                    .OpenBlock();
                    itemHandler.GenerateWrite( fWrite, "e" );
                    fWrite.CloseBlock()
                     .Append( "w.WriteEndArray();" ).NewLine();
                }
                return true;
            }
            return false;
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

        JsonTypeInfo? TryRegisterInfoForEnum( Type t )
        {
            if( !t.GetExternalNames( _monitor, out string name, out string[]? previousNames ) )
            {
                return null;
            }
            var uT = Enum.GetUnderlyingType( t );
            return AllowTypeInfo( t, name, previousNames ).Configure(
                        ( ICodeWriter write, string variableName )
                            => write.Append( "w.WriteNumberValue( (" ).AppendCSharpName( uT ).Append( ')' ).Append( variableName ).Append( " );" ),
                        ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable )
                            =>
                        {
                            read.OpenBlock()
                                .Append( "var " );
                            _map[uT].GenerateRead( read, "u", true );
                            read.NewLine()
                                .Append( variableName ).Append( " = (" ).AppendCSharpName( t ).Append( ")u;" )
                                .CloseBlock();
                        } );
        }

        JsonTypeInfo? TryRegisterInfoForValueTuple( Type t, Type[] types )
        {
            IJsonCodeGenHandler[] handlers = new IJsonCodeGenHandler[types.Length];
            var b = new StringBuilder( "[" );
            for( int i = 0; i < types.Length; i++ )
            {
                if( i > 0 ) b.Append( ',' );
                var h = AllowType( types[i] );
                if( h == null ) return null;
                handlers[i] = h;
                b.Append( h.Name );
            }
            b.Append( ']' );
            JsonTypeInfo info = AllowTypeInfo( t, b.ToString() );

            var valueTupleName = t.ToCSharpName();
            // Don't use 'in' modifier on non-readonly structs: See https://devblogs.microsoft.com/premier-developer/the-in-modifier-and-the-readonly-structs-in-c/
            var fWriteDef = FunctionDefinition.Parse( "internal static void WriteVT_" + info.NumberName + "( System.Text.Json.Utf8JsonWriter w, ref " + valueTupleName + " v )" );
            var fReadDef = FunctionDefinition.Parse( "internal static void ReadVT_" + info.NumberName + "( ref System.Text.Json.Utf8JsonReader r, out " + valueTupleName + " v )" );

            IFunctionScope? fWrite = _pocoDirectory.FindFunction( fWriteDef.Key, false );
            IFunctionScope? fRead;
            if( fWrite != null )
            {
                fRead = _pocoDirectory.FindFunction( fReadDef.Key, false );
                Debug.Assert( fRead != null );
            }
            else
            {
                fWrite = _pocoDirectory.CreateFunction( fWriteDef );
                fWrite.Append( "w.WriteStartArray();" ).NewLine();
                int itemNumber = 0;
                foreach( var h in handlers )
                {
                    h.GenerateWrite( fWrite, "v.Item" + (++itemNumber).ToString( CultureInfo.InvariantCulture ) );
                }
                fWrite.Append( "w.WriteEndArray();" ).NewLine();

                fRead = _pocoDirectory.CreateFunction( fReadDef );
                fRead.Append( "r.Read();" ).NewLine();

                itemNumber = 0;
                foreach( var h in handlers )
                {
                    h.GenerateRead( fRead, "v.Item" + (++itemNumber).ToString( CultureInfo.InvariantCulture ), false );
                }
                fRead.Append( "r.Read();" ).NewLine();
            }

            info.SetByRefWriter()
                .Configure(
                  ( ICodeWriter write, string variableName ) =>
                  {
                      write.Append( "PocoDirectory_CK." ).Append( fWrite.Definition.MethodName.Name ).Append( "( w, ref " ).Append( variableName ).Append( " );" );
                  },
                  ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                  {
                      string vName = variableName;
                      if( isNullable )
                      {
                          read.OpenBlock()
                              .AppendCSharpName( info.Type ).Space().Append( "notNull;" ).NewLine();
                          vName = "notNull";
                      }
                      read.Append( "PocoDirectory_CK." ).Append( fRead.Definition.MethodName.Name ).Append( "( ref r, out " ).Append( vName ).Append( " );" );
                      if( isNullable )
                      {
                          read.Append( variableName ).Append( " = notNull;" )
                              .CloseBlock();
                      }
                  } );

            return info;
        }

        #region Finalization

        /// <summary>
        /// Finalizes the code generation.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>True on success, false on error.</returns>
        public bool FinalizeCodeGeneration( IActivityMonitor monitor )
        {
            using( monitor.OpenInfo( $"Generating Json serialization with {_map.Count} mappings to {_typeInfos.Count} types." ) )
            {
                int missingCount = 0;
                foreach( var t in _typeInfos )
                {
                    if( t.DirectType == JsonDirectType.None && (t.CodeReader == null || t.CodeWriter == null) )
                    {
                        ++missingCount;
                        using( _monitor.OpenTrace( $"Missing CodeReader/Writer for '{t.Name}'. Raising TypeInfoConfigurationRequired." ) )
                        {
                            try
                            {
                                TypeInfoConfigurationRequired?.Invoke( this, new TypeInfoConfigurationRequiredEventArg( _monitor, this, t ) );
                            }
                            catch( Exception ex )
                            {
                                _monitor.Error( $"While raising TypeInfoConfigurationRequired for '{t.Name}'.", ex );
                                return false;
                            }
                        }
                    }
                }
                if( missingCount > 0 )
                {
                    // Let the TypeInfo be configured in any order (the event for Z may have configured A and Z together).
                    var missing = _typeInfos.Where( i => i.CodeWriter == null || i.CodeReader == null ).ToList();
                    if( missing.Count > 0 )
                    {
                        _monitor.Error( $"Missing Json CodeReader/Writer functions for types '{missing.Select( m => m.Name ).Concatenate( "', '" )}'." );
                        return false;
                    }
                }
                // Generates the code for "dynamic"/"untyped" object.

                // Reading must handle the [TypeName,...] array: it needs a lookup from the "type name" to the handler to use: this is the goal of
                // the _typeReaders dictionary that we initialize here (no concurrency issue, no lock here: once built the dictionary will only
                // be read).
                GenerateDynamicRead( _map );

                // Writing must handle the object instance to write. Null reference/value type can be handled immediately (by writing "null").
                // When not null, we are dealing only with concrete types here: the object MUST be of an allowed concrete type, an abstraction
                // that wouldn't be one of the allowed concrete type must NOT be handled!
                // That's why we can use a direct pattern matching on the object's type for the write method (after having ordered types with specializations
                // first to correctly handle base types mapped to a different handler than its specialization).
                GenerateDynamicWrite( _typeInfos );

                monitor.CloseGroup( "Success." );
                return true;
            }
        }

        void GenerateDynamicRead( Dictionary<object, IJsonCodeGenHandler> map )
        {
            _pocoDirectory.GeneratedByComment()
                          .Append( @"
            delegate object ReaderFunction( ref System.Text.Json.Utf8JsonReader r );

            static readonly Dictionary<string, ReaderFunction> _typeReaders = new Dictionary<string, ReaderFunction>();

            internal static object ReadObject( ref System.Text.Json.Utf8JsonReader r )
            {
                switch( r.TokenType )
                {
                    case System.Text.Json.JsonTokenType.Null: r.Read(); return null;
                    case System.Text.Json.JsonTokenType.Number: { var v = r.GetInt32(); r.Read(); return v; }
                    case System.Text.Json.JsonTokenType.String: { var v = r.GetString(); r.Read(); return v; }
                    case System.Text.Json.JsonTokenType.False: { r.Read(); return false; }
                    case System.Text.Json.JsonTokenType.True: { r.Read(); return true; }
                    default:
                        {
                            r.Read(); // [
                            var n = r.GetString();
                            r.Read();
                            if( !_typeReaders.TryGetValue( n, out var reader ) )
                            {
                                throw new System.Text.Json.JsonException( $""Unregistered type name '{n}'."" );
                            }
                            var o = reader( ref r );
                            r.Read(); // ]
                            return o;
                        }
                }
            }
" );

            // Configures the _typeReaders dictionary in the constructor.
            var ctor = _pocoDirectory.FindOrCreateFunction( "public PocoDirectory_CK()" )
                                     .GeneratedByComment();
            foreach( var (typeOrName, handler) in map )
            {
                // Skips direct types that are handled...directly.
                if( handler.Info.DirectType != JsonDirectType.None && handler.Info.DirectType != JsonDirectType.Untyped ) continue;

                // Write is called on the object's concrete type, it is useless to map an abstract type here since Read reads
                // back what has been written...
                if( handler.IsAbstractType ) continue;

                // Only consider names here (Type is for the Write!).
                if( !(typeOrName is string name) ) continue;

                Debug.Assert( name == handler.Name );
                // Reading null is already handled: we can skip any nullable handler.
                // ValueType nullable names don't appear in the map. We add them as an alias.
                if( handler.IsNullable ) continue;

                ctor.OpenBlock()
                    .Append( "ReaderFunction d = delegate( ref System.Text.Json.Utf8JsonReader r ) {" )
                    .AppendCSharpName( handler.Type ).Append( " o;" ).NewLine();
                handler.GenerateRead( ctor, "o", assignOnly: true, skipIfNullBlock: true );
                ctor.NewLine().Append( "return o;" ).NewLine()
                    .Append( "};" ).NewLine();
                ctor.Append( "_typeReaders.Add( " ).AppendSourceString( handler.Name ).Append( ", d );" ).NewLine();
                ctor.Append( "_typeReaders.Add( " ).AppendSourceString( handler.ToNullHandler().Name ).Append( ", d );" ).NewLine();

                ctor.CloseBlock();
            }

        }

        void GenerateDynamicWrite( List<JsonTypeInfo> types )
        {
            int Compare( Type t1, Type t2 )
            {
                if( t1 == t2 ) return 0;
                if( t1.IsValueType )
                {
                    if( t2.IsValueType )
                    {
                        return t1.IsEnum
                                ? (t2.IsEnum ? 0 : 1)
                                : (t2.IsEnum ? -1 : 0);
                    }
                    return -1;
                }
                if( t2.IsValueType ) return 1;
                return t1.IsAssignableFrom( t2 )
                                         ? 1
                                         : (t2.IsAssignableFrom( t1 )
                                            ? -1
                                            : 0);
            }
            Debug.Assert( Compare( typeof( int ), typeof( int ) ) == 0 );
            Debug.Assert( Compare( typeof( JsonDirectType ), typeof( JsonDirectType ) ) == 0 );
            Debug.Assert( Compare( typeof( string ), typeof( string ) ) == 0 );
            Debug.Assert( Compare( typeof( int ), typeof( JsonDirectType ) ) < 0 );
            Debug.Assert( Compare( typeof( JsonDirectType ), typeof( int ) ) > 0 );
            Debug.Assert( Compare( typeof( string ), typeof( int ) ) > 0 );
            Debug.Assert( Compare( typeof( string ), typeof( JsonDirectType ) ) > 0 );
            Debug.Assert( Compare( typeof( int ), typeof( string ) ) < 0 );
            Debug.Assert( Compare( typeof( JsonDirectType ), typeof( string ) ) < 0 );
            Debug.Assert( Compare( typeof( TypeInfoConfigurationRequiredEventArg ), typeof( EventArgs ) ) < 0 );
            Debug.Assert( Compare( typeof( EventArgs ), typeof( TypeInfoConfigurationRequiredEventArg ) ) > 0 );

            _pocoDirectory
                    .GeneratedByComment()
                .Append( @"
internal static void WriteObject( System.Text.Json.Utf8JsonWriter w, object o )
{
    switch( o )
    {
        case null: w.WriteNullValue(); break;
        case string v: w.WriteStringValue( v ); break;
        case int v: w.WriteNumberValue( v ); break;
        case bool v: w.WriteBooleanValue( v ); break;" ).NewLine()
        .CreatePart( out var mappings ).Append( @"
        default: throw new System.Text.Json.JsonException( $""Unregistered type '{o.GetType().AssemblyQualifiedName}'."" );
    }
}" );
            // Ordering is: value types, enums, reference types.
            types.Sort( ( t1, t2 ) => Compare( t1.Type, t2.Type ) );
            foreach( var t in types )
            {
                // Skips direct types.
                if( t.DirectType != JsonDirectType.None ) continue;

                mappings.Append( "case " ).AppendCSharpName( t.Type, useValueTupleParentheses: false ).Append( " v: " );
                Debug.Assert( !t.NonNullHandler.IsAbstractType, "Only concrete Types are JsonTypeInfo, abstract types are just mappings." );
                t.NonNullHandler.GenerateWrite( mappings, "v", withType: true, skipIfNullBlock: true );
                mappings.NewLine().Append( "break;" ).NewLine();
            }
        }

        #endregion
    }
}
