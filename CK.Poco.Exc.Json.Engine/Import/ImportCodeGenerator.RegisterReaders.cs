using CK.CodeGen;
using CK.Core;
using System.Numerics;
using System;
using System.Diagnostics;
using static CK.Core.PocoJsonExportSupport;
using System.Collections.Generic;

namespace CK.Setup.PocoJson
{
    sealed partial class ImportCodeGenerator
    {
        // Step 1: The _readers array is filled with Reader delegates for all Exchangeable and NonNullable types.
        //         Among them, only IPoco and Records require an explicit generation of their methods since collections
        //         are implemented once for all based on the typed functions reader of their item type.
        void RegisterReaders( List<IPrimaryPocoType> pocos, List<IRecordPocoType> records )
        {
            foreach( var type in _nameMap.ExchangeableNonNullableTypes )
            {
                switch( type.Kind )
                {
                    case PocoTypeKind.UnionType:
                    case PocoTypeKind.Any:
                        _readers[type.Index >> 1] = ObjectReader;
                        break;
                    case PocoTypeKind.AbstractIPoco:
                        _readers[type.Index >> 1] = GetAbstractPocoReader( type );
                        break;
                    case PocoTypeKind.IPoco:
                        _readers[type.Index >> 1] = GetPocoReader( type );
                        pocos.Add( (IPrimaryPocoType)type );
                        break;
                    case PocoTypeKind.Basic:
                        _readers[type.Index >> 1] = GetBasicTypeCodeReader( type );
                        break;
                    case PocoTypeKind.Array:
                        {
                            var tA = (ICollectionPocoType)type;
                            _readers[type.Index >> 1] = tA.ItemTypes[0].Type == typeof( byte )
                                                            ? ( w, v ) => w.Append( v ).Append( "=r.GetBytesFromBase64();r.Read();" )
                                                            : GetArrayCodeReader( tA );
                            break;
                        }
                    case PocoTypeKind.List:
                    case PocoTypeKind.HashSet:
                        _readers[type.Index >> 1] = GetListOrSetCodeReader( (ICollectionPocoType)type );
                        break;
                    case PocoTypeKind.Dictionary:
                        _readers[type.Index >> 1] = GetDictionaryCodeReader( (ICollectionPocoType)type );
                        break;
                    case PocoTypeKind.Record:
                    case PocoTypeKind.AnonymousRecord:
                        _readers[type.Index >> 1] = GetRecordCodeReader( type );
                        records.Add( (IRecordPocoType)type );
                        break;
                    case PocoTypeKind.Enum:
                        {
                            var tE = (IEnumPocoType)type;
                            _readers[type.Index >> 1] = ( w, v ) =>
                            {
                                w.OpenBlock()
                                 .Append( "var " );
                                GenerateRead( w, tE.UnderlyingType, "u", false );
                                w.NewLine().Append( v ).Append( "=(" ).Append( tE.CSharpName ).Append( ")u;" )
                                 .CloseBlock();
                            };
                            break;
                        }
                }
            }
            return;

            static void ObjectReader( ICodeWriter writer, string variableName )
            {
                writer.Append( variableName ).Append( "=CK.Poco.Exc.JsonGen.Importer.ReadAny( ref r, options );" );
            }

            static CodeReader GetAbstractPocoReader( IPocoType type )
            {
                return ( w, v ) =>
                {
                    w.Append( v ).Append( "=(" ).Append( type.CSharpName ).Append( ")CK.Poco.Exc.JsonGen.Importer.ReadAny( ref r, options );" );
                };
            }

            static CodeReader GetPocoReader( IPocoType type )
            {
                return ( w, v ) => w.Append( v ).Append( ".ReadJson( ref r, options );" );
            }

            static CodeReader GetBasicTypeCodeReader( IPocoType type )
            {
                if( type.Type == typeof(int) )
                {
                    return (w,v) => w.Append( v ).Append( "=r.GetInt32();r.Read();" );
                }
                if( type.Type == typeof( bool ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "=r.GetBoolean();r.Read();" );
                }
                if( type.Type == typeof( string ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "=r.GetString();r.Read();" );
                }
                if( type.Type == typeof( double ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "=r.GetDouble();r.Read();" );
                }
                if( type.Type == typeof( float ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "=r.GetSingle();r.Read();" );
                }
                if( type.Type == typeof( byte ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "=r.GetByte();r.Read();" );
                }
                if( type.Type == typeof( sbyte ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "=r.GetSByte();r.Read();" );
                }
                if( type.Type == typeof( DateTime ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "=r.GetDateTime();r.Read();" );
                }
                if( type.Type == typeof( DateTimeOffset ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "=r.GetDateTimeOffset();r.Read();" );
                }
                if( type.Type == typeof( TimeSpan ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "=TimeSpan.FromTicks(r.TokenType==System.Text.Json.JsonTokenType.String?Int64.Parse(r.GetString(),System.Globalization.NumberFormatInfo.InvariantInfo):r.GetInt64()); r.Read();" );
                }
                if( type.Type == typeof( short ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "=r.GetInt16();r.Read();" );
                }
                if( type.Type == typeof( ushort ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "=r.GetUInt16();r.Read();" );
                }
                if( type.Type == typeof( BigInteger ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "=System.Numerics.BigInteger.Parse(r.GetString(),System.Globalization.NumberFormatInfo.InvariantInfo );r.Read();" );
                }
                if( type.Type == typeof( long ) )
                {
                    // Challenge the data itself (apply Postel's law, see https://en.wikipedia.org/wiki/Robustness_principle).
                    // WHY is the Utf8JsonReader has a perfect GetInt64WithQuotes() that is internal?
                    // Because Microsoft guys don't want you to be able to do the same as them... :(
                    return ( w, v ) => w.Append( v ).Append( "=r.TokenType==System.Text.Json.JsonTokenType.String?Int64.Parse(r.GetString(),System.Globalization.NumberFormatInfo.InvariantInfo):r.GetInt64();r.Read();" );
                }
                if( type.Type == typeof( ulong ) )
                {
                    // Challenge the data itself (apply Postel's law, see https://en.wikipedia.org/wiki/Robustness_principle).
                    // WHY is the Utf8JsonReader has a perfect GetUInt64WithQuotes() that is internal?
                    // Because Microsoft guys don't want you to be able to do the same as them... :(
                    return ( w, v ) => w.Append( v ).Append( "=r.TokenType==System.Text.Json.JsonTokenType.String?UInt64.Parse(r.GetString(),System.Globalization.NumberFormatInfo.InvariantInfo):r.GetUInt64();r.Read();" );
                }
                if( type.Type == typeof( Guid ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "=r.GetGuid();r.Read();" );
                }
                if( type.Type == typeof( uint ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "=r.GetUInt32();r.Read();" );
                }
                if( type.Type == typeof( decimal ) )
                {
                    // Challenge the data itself (apply Postel's law, see https://en.wikipedia.org/wiki/Robustness_principle).
                    // WHY is the Utf8JsonReader has a perfect  GetUInt64WithQuotes() that is internal?
                    // Because Microsoft guys don't want you to be able to do the same as them... :(
                    return ( w, v ) => w.Append( v ).Append( "=r.TokenType==System.Text.Json.JsonTokenType.String?Decimal.Parse(r.GetString(),System.Globalization.NumberFormatInfo.InvariantInfo ):r.GetDecimal();r.Read();" );
                }
                return Throw.NotSupportedException<CodeReader>();
            }

            CodeReader GetArrayCodeReader( ICollectionPocoType type )
            {
                var readerFunction = GetReadFunctionName( type.ItemTypes[0] );
                return ( writer, v ) => writer.Append( v ).Append( "=CK.Poco.Exc.JsonGen.Importer.ReadArray(ref r," )
                                              .Append( readerFunction )
                                              .Append( ",options);" );
            }

            CodeReader GetListOrSetCodeReader( ICollectionPocoType type )
            {
                var readerFunction = GetReadFunctionName( type.ItemTypes[0] );
                return ( writer, v ) => writer.Append( "CK.Poco.Exc.JsonGen.Importer.FillListOrSet(ref r," )
                                              .Append( v )
                                              .Append( "," )
                                              .Append( readerFunction )
                                              .Append( ",options);" );
            }

            CodeReader GetDictionaryCodeReader( ICollectionPocoType type )
            {
                var keyType = type.ItemTypes[0];
                var valueFunction = GetReadFunctionName( type.ItemTypes[1] );
                if( keyType.Type == typeof( string ) )
                {
                    return ( writer, v ) => writer.Append( "CK.Poco.Exc.JsonGen.Importer.FillDynamicObject(ref r," )
                                                  .Append( v )
                                                  .Append( "," )
                                                  .Append( valueFunction )
                                                  .Append( ",options);" );
                }
                var keyFunction = GetReadFunctionName( type.ItemTypes[0] );
                return ( writer, v ) => writer.Append( "CK.Poco.Exc.JsonGen.Importer.FillDictionary(ref r," )
                                              .Append( v )
                                              .Append( "," )
                                              .Append( keyFunction )
                                              .Append( "," )
                                              .Append( valueFunction )
                                              .Append( ",options);" );
            }

            static CodeReader GetRecordCodeReader( IPocoType type )
            {
                Debug.Assert( type.Kind == PocoTypeKind.Record || type.Kind == PocoTypeKind.AnonymousRecord );
                return ( w, v ) => w.Append( "CK.Poco.Exc.JsonGen.Importer.Read_" )
                                    .Append( type.Index )
                                    .Append( "(ref r,ref " )
                                    .Append( v ).Append( ",options);" );
            }

        }
    }
}
