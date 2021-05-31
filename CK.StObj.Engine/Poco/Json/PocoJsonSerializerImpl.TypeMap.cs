using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

#nullable enable

namespace CK.Setup
{
    public partial class PocoJsonSerializerImpl
    {
        readonly Dictionary<object, IHandler> _map = new Dictionary<object, IHandler>();
        int _typeInfoCurrentCount;

        /// <summary>
        /// Generates a new pre-registration key.
        /// </summary>
        /// <param name="t">The type for which a key must be obtained.</param>
        /// <returns>A new registration key.</returns>
        public TypeInfo.RegKey GetNextRegKey( Type t )
        {
            return new TypeInfo.RegKey( t, (_typeInfoCurrentCount++).ToString() );
        }

        TypeInfo AddTypeInfo( Type t, string name, IReadOnlyList<string>? previousNames = null, DirectType d = DirectType.None, bool isAbstractType = false )
        {
            return AddTypeInfoToTheMap( new TypeInfo( GetNextRegKey( t ), name, previousNames, d, isAbstractType ) );
        }

        /// <summary>
        /// Registers the <see cref="TypeInfo.Type"/>, the <see cref="TypeInfo.Name"/> and all <see cref="TypeInfo.PreviousNames"/>
        /// onto the <see cref="TypeInfo.NonNullHandler"/> and if the type is a value type, its Nullable&lt;Type&gt; is mapped to
        /// the <see cref="TypeInfo.NullHandler"/>.
        /// </summary>
        /// <param name="i">The TypeInfo to register.</param>
        /// <returns>The registered TypeInfo.</returns>
        TypeInfo AddTypeInfoToTheMap( TypeInfo i )
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
            return i;
        }

        void AddTypeHandlerAlias( Type t, IHandler handler )
        {
            _map.Add( t, handler.CreateAbstract( t ) );
        }

        void AddUntypedHandler( Type t, bool nullable = true )
        {
            _map.Add( t, nullable ? TypeInfo.Untyped.NullHandler.CreateAbstract( t ) : TypeInfo.Untyped.NonNullHandler.CreateAbstract( t ) );
        }

        void InitializeMap()
        {
            // Direct types.
            AddTypeInfoToTheMap( TypeInfo.Untyped );
            AddTypeInfo( typeof( int ), "int", null, DirectType.Int );
            AddTypeInfo( typeof( bool ), "bool", null, DirectType.Bool );
            AddTypeInfo( typeof( string ), "string", null, DirectType.String );

            static void WriteString( ICodeWriter write, string variableName )
            {
                write.Append( "w.WriteStringValue( " ).Append( variableName ).Append( " );" );
            }

            static void WriteNumber( ICodeWriter write, string variableName )
            {
                write.Append( "w.WriteNumberValue( " ).Append( variableName ).Append( " );" );
            }

            AddTypeInfo( typeof( byte[] ), "byte[]" ).Configure(
                ( ICodeWriter write, string variableName ) =>
                {
                    write.Append( "w.WriteBase64StringValue( " ).Append( variableName ).Append( " );" );
                },
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetBytesFromBase64(); r.Read();" );
                } );

            AddTypeInfo( typeof( Guid ), "g" ).Configure( WriteString,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetGuid(); r.Read();" );
                } );

            AddTypeInfo( typeof( Decimal ), "Decimal" ).Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetDecimal(); r.Read();" );
                } );

            AddTypeInfo( typeof( uint ), "uint" ).Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetUInt32(); r.Read();" );
                } );

            AddTypeInfo( typeof( double ), "double" ).Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetDouble(); r.Read();" );
                } );

            AddTypeInfo( typeof( float ), "float" ).Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetSingle(); r.Read();" );
                } );

            AddTypeInfo( typeof( long ), "long" ).Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetInt64(); r.Read();" );
                } );

            AddTypeInfo( typeof( ulong ), "ulong" ).Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetUInt64(); r.Read();" );
                } );

            AddTypeInfo( typeof( byte ), "byte" ).Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetByte(); r.Read();" );
                } );

            AddTypeInfo( typeof( sbyte ), "sbyte" ).Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetSByte(); r.Read();" );
                } );

            AddTypeInfo( typeof( short ), "short" ).Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetInt16(); r.Read();" );
                } );

            AddTypeInfo( typeof( ushort ), "ushort" ).Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetUInt16(); r.Read();" );
                } );

            AddTypeInfo( typeof( DateTime ), "DateTime" ).Configure( WriteString,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetDateTime(); r.Read();" );
                } );

            AddTypeInfo( typeof( DateTimeOffset ), "DateTimeOffset" ).Configure( WriteString,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetDateTimeOffset(); r.Read();" );
                } );

            AddTypeInfo( typeof( TimeSpan ), "TimeSpan" ).Configure(
                ( ICodeWriter write, string variableName ) =>
                {
                    write.Append( "w.WriteNumberValue( " ).Append( variableName ).Append( ".Ticks );" );
                },
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = TimeSpan.FromTicks( r.GetInt64() ); r.Read();" );
                } );

        }

        IHandler? TryFindOrCreateHandler( Type t, bool? nullableHandler = null )
        {
            if( !_map.TryGetValue( t, out var handler ) )
            {
                TypeInfo? info = null;
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
                    TypeInfo.RegKey reg;
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
                        reg = GetNextRegKey( t );
                        (fWrite, fRead, info) = CreateListOrSetFunctions( reg, isList );
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
                        reg = GetNextRegKey( t );
                        if( tKey == typeof( string ) )
                        {
                            (fWrite, fRead, info) = CreateStringMapFunctions( reg, tValue );
                        }
                        else
                        {
                            (fWrite, fRead, info) = CreateMapFunctions( reg, tKey, tValue );
                        }

                    }
                    if( info != null )
                    {
                        Debug.Assert( fRead != null && fWrite != null && tInterface != null );
                        handler = ConfigureAndAddTypeInfo( info, tInterface, fWrite, fRead );
                    }
                }
            }
            if( handler == null )
            {
                Monitor.Error( $"Unable to handle Json serialization for type '{t}'." );
            }
            else
            {
                // Honor the optional null/not null handler (override the default handler type).
                if( nullableHandler != null )
                {
                    handler = nullableHandler.Value ? handler.ToNullHandler() : handler.ToNonNullHandler();
                }
            }
            return handler;
        }

        IHandler ConfigureAndAddTypeInfo( TypeInfo info, Type tInterface, IFunctionScope fWrite, IFunctionScope fRead )
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
            AddTypeInfoToTheMap( info );
            // The interface is directly mapped to the non null handler.
            AddTypeHandlerAlias( tInterface, info.NonNullHandler );
            return info.NullHandler;
        }

        (IFunctionScope fWrite, IFunctionScope fRead, TypeInfo info) CreateMapFunctions( in TypeInfo.RegKey reg, Type tKey, Type tValue )
        {
            var keyHandler = TryFindOrCreateHandler( tKey );
            if( keyHandler == null ) return default;
            var valueHandler = TryFindOrCreateHandler( tValue );
            if( valueHandler == null ) return default;

            string keyTypeName = keyHandler.Type.ToCSharpName();
            string valueTypeName = valueHandler.Type.ToCSharpName();
            var concreteTypeName = "Dictionary<" + keyTypeName + "," + valueTypeName + ">";

            string funcSuffix = keyHandler.Info.NumberName + "_" + valueHandler.Info.NumberName;
            // Trick: the reader/writer functions accepts the interface rather than the concrete type.
            var fWriteDef = FunctionDefinition.Parse( "internal static void WriteM_" + funcSuffix + "( System.Text.Json.Utf8JsonWriter w, I" + concreteTypeName + " c )" );
            var fReadDef = FunctionDefinition.Parse( "internal static void ReadM_" + funcSuffix + "( ref System.Text.Json.Utf8JsonReader r, I" + concreteTypeName + " c )" );
            IFunctionScope? fWrite = PocoDirectory.FindFunction( fWriteDef.Key, false );
            IFunctionScope? fRead;
            if( fWrite != null )
            {
                fRead = PocoDirectory.FindFunction( fReadDef.Key, false );
                Debug.Assert( fRead != null );
            }
            else
            {
                fWrite = PocoDirectory.CreateFunction( fWriteDef );
                fWrite.Append( "w.WriteStartArray();" ).NewLine()
                      .Append( "foreach( var e in c )" )
                      .OpenBlock()
                      .Append( "w.WriteStartArray();" ).NewLine();

                keyHandler.GenerateWrite( fWrite, "e.Key" );
                valueHandler.GenerateWrite( fWrite, "e.Value" );

                fWrite.Append( "w.WriteEndArray();" )
                      .CloseBlock()
                      .Append( "w.WriteEndArray();" ).NewLine();

                fRead = PocoDirectory.CreateFunction( fReadDef );
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
            return (fWrite, fRead, new TypeInfo( in reg, "M(" + keyHandler.Name + "," + valueHandler.Name + ")" ));
        }

        (IFunctionScope fWrite, IFunctionScope fRead, TypeInfo info) CreateStringMapFunctions( in TypeInfo.RegKey reg, Type tValue )
        {
            var valueHandler = TryFindOrCreateHandler( tValue );
            if( valueHandler == null ) return default;

            string valueTypeName = valueHandler.Type.ToCSharpName();
            var concreteTypeName = "Dictionary<string," + valueTypeName + ">";
            var fWriteDef = FunctionDefinition.Parse( "internal static void WriteO_" + valueHandler.Info.NumberName + "( System.Text.Json.Utf8JsonWriter w, I" + concreteTypeName + " c )" );
            var fReadDef = FunctionDefinition.Parse( "internal static void ReadO_" + valueHandler.Info.NumberName + "( ref System.Text.Json.Utf8JsonReader r, I" + concreteTypeName + " c )" );
            IFunctionScope? fWrite = PocoDirectory.FindFunction( fWriteDef.Key, false );
            IFunctionScope? fRead;
            if( fWrite != null )
            {
                fRead = PocoDirectory.FindFunction( fReadDef.Key, false );
                Debug.Assert( fRead != null );
            }
            else
            {
                fWrite = PocoDirectory.CreateFunction( fWriteDef );
                fWrite.Append( "w.WriteStartObject();" ).NewLine()
                      .Append( "foreach( var e in c )" )
                      .OpenBlock()
                      .Append( "w.WritePropertyName( e.Key );" );
                valueHandler.GenerateWrite( fWrite, "e.Value" );
                fWrite.CloseBlock()
                 .Append( "w.WriteEndObject();" ).NewLine();

                fRead = PocoDirectory.CreateFunction( fReadDef );
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
            return (fWrite, fRead, new TypeInfo( in reg, "O<" + valueHandler.Name + ">" ));
        }

        (IFunctionScope fWrite, IFunctionScope fRead, TypeInfo info) CreateListOrSetFunctions( in TypeInfo.RegKey reg, bool isList )
        {
            Type tItem = reg.Type.GetGenericArguments()[0];
            var itemHandler = TryFindOrCreateHandler( tItem );
            if( itemHandler == null ) return default;

            string itemTypeName = itemHandler.Type.ToCSharpName();
            var fWriteDef = FunctionDefinition.Parse( "internal static void WriteLOrS_" + itemHandler.Info.NumberName + "( System.Text.Json.Utf8JsonWriter w, ICollection<" + itemTypeName + "> c )" );
            var fReadDef = FunctionDefinition.Parse( "internal static void ReadLOrS_" + itemHandler.Info.NumberName + "( ref System.Text.Json.Utf8JsonReader r, ICollection<" + itemTypeName + "> c )" );

            IFunctionScope? fWrite = PocoDirectory.FindFunction( fWriteDef.Key, false );
            IFunctionScope? fRead;
            if( fWrite != null )
            {
                fRead = PocoDirectory.FindFunction( fReadDef.Key, false );
                Debug.Assert( fRead != null );
            }
            else
            {
                fWrite = PocoDirectory.CreateFunction( fWriteDef );
                fWrite.Append( "w.WriteStartArray();" ).NewLine()
                .Append( "foreach( var e in c )" )
                .OpenBlock();
                itemHandler.GenerateWrite( fWrite, "e" );
                fWrite.CloseBlock()
                 .Append( "w.WriteEndArray();" ).NewLine();

                fRead = PocoDirectory.CreateFunction( fReadDef );
                fRead.Append( "r.Read();" ).NewLine()
                     .AppendCSharpName( tItem ).Append( " v;" ).NewLine()
                     .Append( "while( r.TokenType != System.Text.Json.JsonTokenType.EndArray )" )
                     .OpenBlock();
                itemHandler.GenerateRead( fRead, "v", false );
                fRead.Append( "c.Add( v );" )
                     .CloseBlock()
                     .Append( "r.Read();" );
            }

            return (fWrite, fRead, new TypeInfo( in reg, (isList ? "L(" : "S(") + itemHandler.Name + ")" ));
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

        TypeInfo? TryRegisterInfoForEnum( Type t )
        {
            if( !t.GetExternalNames( Monitor, out string name, out string[]? previousNames ) )
            {
                return null;
            }
            var uT = Enum.GetUnderlyingType( t );
            return AddTypeInfo( t, name, previousNames ).Configure(
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


        TypeInfo? TryRegisterInfoForValueTuple( Type t, Type[] types )
        {
            IHandler[] handlers = new IHandler[types.Length];
            var b = new StringBuilder( "[" );
            for( int i = 0; i < types.Length; i++ )
            {
                if( i > 0 ) b.Append( ',' );
                var h = TryFindOrCreateHandler( types[i] );
                if( h == null ) return null;
                handlers[i] = h;
                b.Append( h.Name );
            }
            b.Append( ']' );
            TypeInfo info = AddTypeInfo( t, b.ToString() );

            var valueTupleName = t.ToCSharpName();
            // Don't use 'in' modifier on non-readonly structs: See https://devblogs.microsoft.com/premier-developer/the-in-modifier-and-the-readonly-structs-in-c/
            var fWriteDef = FunctionDefinition.Parse( "internal static void WriteVT_" + info.NumberName + "( System.Text.Json.Utf8JsonWriter w, ref " + valueTupleName + " v )" );
            var fReadDef = FunctionDefinition.Parse( "internal static void ReadVT_" + info.NumberName + "( ref System.Text.Json.Utf8JsonReader r, out " + valueTupleName + " v )" );

            IFunctionScope? fWrite = PocoDirectory.FindFunction( fWriteDef.Key, false );
            IFunctionScope? fRead;
            if( fWrite != null )
            {
                fRead = PocoDirectory.FindFunction( fReadDef.Key, false );
                Debug.Assert( fRead != null );
            }
            else
            {
                fWrite = PocoDirectory.CreateFunction( fWriteDef );
                fWrite.Append( "w.WriteStartArray();" ).NewLine();
                int itemNumber = 0;
                foreach( var h in handlers )
                {
                    h.GenerateWrite( fWrite, "v.Item" + (++itemNumber).ToString( CultureInfo.InvariantCulture ) );
                }
                fWrite.Append( "w.WriteEndArray();" ).NewLine();

                fRead = PocoDirectory.CreateFunction( fReadDef );
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

    }
}
