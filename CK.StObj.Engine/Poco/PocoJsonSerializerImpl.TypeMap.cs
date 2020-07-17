using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

#nullable enable

namespace CK.Setup
{
    public partial class PocoJsonSerializerImpl
    {
        readonly Dictionary<object, IHandler> _map = new Dictionary<object, IHandler>();
        int _typeInfoCurrentCount;

        public TypeInfo.RegKey GetNextRegKey( Type t )
        {
            return new TypeInfo.RegKey( t, (_typeInfoCurrentCount++).ToString() );
        }

        TypeInfo AddTypeInfo( Type t, string name, IReadOnlyList<string>? previousNames = null, DirectType d = DirectType.None, bool isAbstractType = false )
        {
            return AddTypeInfo( new TypeInfo( GetNextRegKey( t ), name, previousNames, d, isAbstractType ) );
        }

        TypeInfo AddTypeInfo( TypeInfo i )
        {
            bool isValueType = i.Type.IsValueType;
            var defaultHandler = isValueType ? i.NotNullHandler : i.NullHandler;
            _map.Add( i.Type, defaultHandler );
            _map.Add( i.Name, defaultHandler );
            foreach( var p in i.PreviousNames )
            {
                _map.Add( p, defaultHandler );
            }
            if( isValueType )
            {
                _map.Add( i.NullHandler.Type, i.NullHandler );
                Debug.Assert( i.NullHandler.Name != i.NotNullHandler.Name );
                // Registering the nullable "name?" is useless: FillDynamicMap will do the job.
                //_map.Add( i.NullHandler.Name, i.NullHandler );
            }
            return i;
        }

        void AddTypeHandlerAlias( Type t, IHandler handler )
        {
            _map.Add( t, handler.CreateAbstract( t ) );
        }

        void AddUntypedNullHandler( Type t, bool nullable = true )
        {
            _map.Add( t, nullable ? TypeInfo.Untyped.NullHandler : TypeInfo.Untyped.NotNullHandler );
        }

        void InitializeMap()
        {
            // Direct types.
            AddTypeInfo( TypeInfo.Untyped );
            AddTypeInfo( typeof( int ), "int", null, DirectType.Int );
            AddTypeInfo( typeof( bool ), "bool", null, DirectType.Bool );
            AddTypeInfo( typeof( string ), "string", null, DirectType.String );

            static void WriteString( ICodeWriter write, string variableName, string pocoDirectoryAccessor )
            {
                write.Append( "w.WriteStringValue( " ).Append( variableName ).Append( " );" );
            }

            static void WriteNumber( ICodeWriter write, string variableName, string pocoDirectoryAccessor )
            {
                write.Append( "w.WriteNumberValue( " ).Append( variableName ).Append( " );" );
            }

            AddTypeInfo( typeof( byte[] ), "byte[]" ).Configure(
                ( ICodeWriter write, string variableName, string pocoDirectoryAccessor ) =>
                {
                    write.Append( "w.WriteBase64StringValue( " ).Append( variableName ).Append( " );" );
                },
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable, string pocoDirectoryAccessor ) =>
                {
                    read.Append( variableName ).Append( " = r.GetBytesFromBase64(); r.Read();" );
                } );

            AddTypeInfo( typeof( Guid ), "g" ).Configure( WriteString,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable, string pocoDirectoryAccessor ) =>
                {
                    read.Append( variableName ).Append( " = r.GetGuid(); r.Read();" );
                } );

            AddTypeInfo( typeof( Decimal ), "Decimal" ).Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable, string pocoDirectoryAccessor ) =>
                {
                    read.Append( variableName ).Append( " = r.GetDecimal(); r.Read();" );
                } );

            AddTypeInfo( typeof( uint ), "uint" ).Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable, string pocoDirectoryAccessor ) =>
                {
                    read.Append( variableName ).Append( " = r.GetUInt32(); r.Read();" );
                } );

            AddTypeInfo( typeof( double ), "double" ).Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable, string pocoDirectoryAccessor ) =>
                {
                    read.Append( variableName ).Append( " = r.GetDouble(); r.Read();" );
                } );

            AddTypeInfo( typeof( float ), "float" ).Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable, string pocoDirectoryAccessor ) =>
                {
                    read.Append( variableName ).Append( " = r.GetSingle(); r.Read();" );
                } );

            AddTypeInfo( typeof( long ), "long" ).Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable, string pocoDirectoryAccessor ) =>
                {
                    read.Append( variableName ).Append( " = r.GetInt64(); r.Read();" );
                } );

            AddTypeInfo( typeof( ulong ), "ulong" ).Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable, string pocoDirectoryAccessor ) =>
                {
                    read.Append( variableName ).Append( " = r.GetUInt64(); r.Read();" );
                } );

            AddTypeInfo( typeof( byte ), "byte" ).Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable, string pocoDirectoryAccessor ) =>
                {
                    read.Append( variableName ).Append( " = r.GetByte(); r.Read();" );
                } );

            AddTypeInfo( typeof( sbyte ), "sbyte" ).Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable, string pocoDirectoryAccessor ) =>
                {
                    read.Append( variableName ).Append( " = r.GetSByte(); r.Read();" );
                } );

            AddTypeInfo( typeof( short ), "short" ).Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable, string pocoDirectoryAccessor ) =>
                {
                    read.Append( variableName ).Append( " = r.GetInt16(); r.Read();" );
                } );

            AddTypeInfo( typeof( ushort ), "ushort" ).Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable, string pocoDirectoryAccessor ) =>
                {
                    read.Append( variableName ).Append( " = r.GetUInt16(); r.Read();" );
                } );

            AddTypeInfo( typeof( DateTime ), "DateTime" ).Configure( WriteString,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable, string pocoDirectoryAccessor ) =>
                {
                    read.Append( variableName ).Append( " = r.GetDateTime(); r.Read();" );
                } );

            AddTypeInfo( typeof( DateTimeOffset ), "DateTimeOffset" ).Configure( WriteString,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable, string pocoDirectoryAccessor ) =>
                {
                    read.Append( variableName ).Append( " = r.GetDateTimeOffset(); r.Read();" );
                } );
        
            AddTypeInfo( typeof( TimeSpan ), "TimeSpan" ).Configure(
                ( ICodeWriter write, string variableName, string pocoDirectoryAccessor ) =>
                {
                    write.Append( "w.WriteNumberValue( " ).Append( variableName ).Append( ".Ticks );" );
                },
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable, string pocoDirectoryAccessor ) =>
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
                    if( info != null )
                    {
                        handler = isNullable ? info.NullHandler : info.NotNullHandler;
                    }
                }
                else if( t.IsGenericType )
                {
                    TypeInfo.RegKey reg = default;
                    IFunctionScope? fWrite = null;
                    IFunctionScope? fRead = null;
                    string? concreteTypeName = null;

                    Type genType = t.GetGenericTypeDefinition();
                    bool isList = genType == typeof( IList<> ) || genType == typeof( List<> );
                    bool isSet = !isList && (genType == typeof( ISet<> ) || genType == typeof( HashSet<> ));
                    if( isList || isSet )
                    {
                        reg = GetNextRegKey( t );
                        (fWrite, fRead, info, concreteTypeName) = CreateListOrSetFunctions( reg, isList );
                    }
                    else if( genType == typeof( IDictionary<,> ) || genType == typeof( Dictionary<,> ) )
                    {
                        reg = GetNextRegKey( t );
                        Type tKey = t.GetGenericArguments()[0];
                        Type tValue = t.GetGenericArguments()[1];
                        if( tKey == typeof( string ) )
                        {
                            (fWrite, fRead, info, concreteTypeName) = CreateStringMapFunctions( reg, tValue );
                        }
                        else
                        {
                            (fWrite, fRead, info, concreteTypeName) = CreateMapFunctions( reg, tKey, tValue );
                        }

                    }
                    if( info != null )
                    {
                        Debug.Assert( fRead != null && fWrite != null && concreteTypeName != null );
                        info = ConfigureAndAddTypeInfo( info, fWrite, fRead, concreteTypeName );
                        handler = info.NullHandler;
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
                    handler = nullableHandler.Value ? handler.Info.NullHandler : handler.Info.NotNullHandler;
                }
            }
            return handler;
        }

        TypeInfo ConfigureAndAddTypeInfo( TypeInfo info, IFunctionScope fWrite, IFunctionScope fRead, string concreteTypeName )
        {
            info.Configure(
                      ( ICodeWriter write, string variableName, string pocoDirectoryAccessor ) =>
                      {
                          write.Append( pocoDirectoryAccessor ).Append( "." ).Append( fWrite.Definition.MethodName.Name ).Append( "( w, " ).Append( variableName ).Append( " );" );
                      },
                      ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable, string pocoDirectoryAccessor ) =>
                      {
                          if( !assignOnly )
                          {
                              if( isNullable )
                              {
                                  read.Append( "if( " ).Append( variableName ).Append( " == null )" )
                                      .OpenBlock()
                                      .Append( variableName ).Append( " = new " ).Append( concreteTypeName ).Append( "();" )
                                      .CloseBlock();
                              }
                              else
                              {
                                  read.Append( variableName ).Append( ".Clear();" ).NewLine();
                              }
                          }
                          else
                          {
                              read.Append( variableName ).Append( " = new " ).Append( concreteTypeName ).Append( "();" ).NewLine();
                          }
                          read.Append( pocoDirectoryAccessor ).Append( "." ).Append( fRead.Definition.MethodName.Name ).Append( "( ref r, " ).Append( variableName ).Append( " );" );
                      } );
            return AddTypeInfo( info );
        }

        (IFunctionScope fWrite, IFunctionScope fRead, TypeInfo info, string concreteTypeName) CreateMapFunctions( in TypeInfo.RegKey reg, Type tKey, Type tValue )
        {
            var keyHandler = TryFindOrCreateHandler( tKey );
            if( keyHandler == null ) return default;
            var valueHandler = TryFindOrCreateHandler( tValue );
            if( valueHandler == null ) return default;

            string keyTypeName = keyHandler.Type.ToCSharpName();
            string valueTypeName = valueHandler.Type.ToCSharpName();
            var concreteTypeName = "Dictionary<" + keyTypeName + "," + valueTypeName + ">";

            string funcPrefix = keyHandler.Info.NumberName + "_" + valueHandler.Info.NumberName;
            var fWriteDef = FunctionDefinition.Parse( "internal void WriteO" + funcPrefix + "( System.Text.Json.Utf8JsonWriter w, I" + concreteTypeName + " c )" );
            var fReadDef = FunctionDefinition.Parse( "internal void ReadO" + funcPrefix + "( ref System.Text.Json.Utf8JsonReader r, I" + concreteTypeName + " c )" );
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

                keyHandler.GenerateWrite( fWrite, "e.Key", FromPocoDirectory );
                valueHandler.GenerateWrite( fWrite, "e.Value", FromPocoDirectory );

                fWrite.Append( "w.WriteEndArray();" )
                      .CloseBlock()
                      .Append( "w.WriteEndArray();" ).NewLine();

                fRead = PocoDirectory.CreateFunction( fReadDef );
                fRead.Append( "r.Read();" ).NewLine()
                     .Append( "while( r.TokenType != System.Text.Json.JsonTokenType.EndArray)" )
                     .OpenBlock()
                     .Append( "r.Read();" ).NewLine();

                fRead.AppendCSharpName( tKey ).Append( " k;" ).NewLine();
                keyHandler.GenerateRead( fRead, "k", true, FromPocoDirectory );

                fRead.NewLine()
                     .AppendCSharpName( tValue ).Append( " v;" ).NewLine();
                valueHandler.GenerateRead( fRead, "v", true, FromPocoDirectory );

                fRead.Append( "r.Read();" ).NewLine()
                     .Append( "c.Add( k, v );" )
                     .CloseBlock()
                     .Append( "r.Read();" );

            }
            return (fWrite, fRead, new TypeInfo( in reg, "!M<" + keyHandler.Name + "," + valueHandler.Name + ">"), concreteTypeName );
        }

        (IFunctionScope fWrite, IFunctionScope fRead, TypeInfo info, string concreteTypeName ) CreateStringMapFunctions( in TypeInfo.RegKey reg, Type tValue )
        {
            var valueHandler = TryFindOrCreateHandler( tValue );
            if( valueHandler == null ) return default;

            string valueTypeName = valueHandler.Type.ToCSharpName();
            var concreteTypeName = "Dictionary<string," + valueTypeName + ">";
            var fWriteDef = FunctionDefinition.Parse( "internal void WriteO" + valueHandler.Info.NumberName + "( System.Text.Json.Utf8JsonWriter w, I" + concreteTypeName + " c )" );
            var fReadDef = FunctionDefinition.Parse( "internal void ReadO" + valueHandler.Info.NumberName + "( ref System.Text.Json.Utf8JsonReader r, I" + concreteTypeName + " c )" );
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
                valueHandler.GenerateWrite( fWrite, "e.Value", FromPocoDirectory );
                fWrite.CloseBlock()
                 .Append( "w.WriteEndObject();" ).NewLine();

                fRead = PocoDirectory.CreateFunction( fReadDef );
                fRead.Append( "r.Read();" ).NewLine()
                    .AppendCSharpName( tValue ).Append( " v;" ).NewLine()
                    .Append( "while( r.TokenType != System.Text.Json.JsonTokenType.EndObject )" )
                    .OpenBlock()
                    .Append( "string k = r.GetString();" ).NewLine()
                    .Append( "r.Read();" ).NewLine();
                valueHandler.GenerateRead( fRead, "v", false, FromPocoDirectory );
                fRead.Append( "c.Add( k, v );" )
                     .CloseBlock()
                     .Append( "r.Read();" );
            }
            return (fWrite, fRead, new TypeInfo( in reg, "!O<" + valueHandler.Name + ">" ), concreteTypeName );
        }

        (IFunctionScope fWrite, IFunctionScope fRead, TypeInfo info, string concreteTypeName) CreateListOrSetFunctions( in TypeInfo.RegKey reg, bool isList )
        {
            Type tItem = reg.Type.GetGenericArguments()[0];
            var itemHandler = TryFindOrCreateHandler( tItem );
            if( itemHandler == null ) return default;

            string itemTypeName = itemHandler.Type.ToCSharpName();
            var fWriteDef = FunctionDefinition.Parse( "internal void WriteLOrS" + itemHandler.Info.NumberName + "( System.Text.Json.Utf8JsonWriter w, ICollection<" + itemTypeName + "> c )" );
            var fReadDef = FunctionDefinition.Parse( "internal void ReadLOrS" + itemHandler.Info.NumberName + "( ref System.Text.Json.Utf8JsonReader r, ICollection<" + itemTypeName + "> c )" );

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
                itemHandler.GenerateWrite( fWrite, "e", FromPocoDirectory );
                fWrite.CloseBlock()
                 .Append( "w.WriteEndArray();" ).NewLine();

                fRead = PocoDirectory.CreateFunction( fReadDef );
                fRead.Append( "r.Read();" ).NewLine()
                     .AppendCSharpName( tItem ).Append( " v;" ).NewLine()
                     .Append( "while( r.TokenType != System.Text.Json.JsonTokenType.EndArray )" )
                     .OpenBlock();
                itemHandler.GenerateRead( fRead, "v", false, FromPocoDirectory );
                fRead.Append( "c.Add( v );" )
                     .CloseBlock()
                     .Append( "r.Read();" );
            }
            var concrete = (isList ? "List<" : "HashSet<") + itemTypeName + ">";
            return (fWrite, fRead, new TypeInfo( in reg, "!" + (isList ? "L<" : "S<") + itemHandler.Name + ">" ), concrete );
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
                        ( ICodeWriter write, string variableName, string pocoDirectoryAccessor )
                            => write.Append( "w.WriteNumberValue( (" ).AppendCSharpName( uT ).Append( ')' ).Append( variableName ).Append( " );" ),
                        ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable, string pocoDirectoryAccessor )
                            =>
                        {
                            read.Append( "(" ).AppendCSharpName( t ).Append( ")" );
                            _map[uT].GenerateRead( read, variableName, false, pocoDirectoryAccessor );
                        } );
        }
    }
}
