using CK.CodeGen;
using CK.Core;
using System.Numerics;
using System;
using System.Diagnostics;
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
            foreach( var type in _nameMap.TypeSet.NonNullableTypes )
            {
                switch( type.Kind )
                {
                    case PocoTypeKind.UnionType:
                    case PocoTypeKind.Any:
                        _readers[type.Index >> 1] = ObjectReader;
                        break;
                    case PocoTypeKind.AbstractPoco:
                        _readers[type.Index >> 1] = GetAbstractPocoReader( type );
                        break;
                    case PocoTypeKind.PrimaryPoco:
                        {
                            var r = GetPocoReader( type );
                            var t = (IPrimaryPocoType)type;
                            _readers[type.Index >> 1] = r;
                            foreach( var sec in t.SecondaryTypes )
                            {
                                _readers[sec.Index >> 1] = r;
                            }
                            pocos.Add( t );
                            break;
                        }
                    case PocoTypeKind.Basic:
                        _readers[type.Index >> 1] = GetBasicTypeCodeReader( type );
                        break;
                    case PocoTypeKind.Array:
                        {
                            var tA = (ICollectionPocoType)type;
                            _readers[type.Index >> 1] = tA.ItemTypes[0].Type == typeof( byte )
                                                            ? ( w, v ) => w.Append( v ).Append( "=r.GetBytesFromBase64();if(!r.Read())rCtx.ReadMoreData(ref r);" )
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
                writer.Append( variableName ).Append( "=CK.Poco.Exc.JsonGen.Importer.ReadAny( ref r, rCtx );" );
            }

            static CodeReader GetAbstractPocoReader( IPocoType type )
            {
                return ( w, v ) =>
                {
                    w.Append( v ).Append( "=(" ).Append( type.CSharpName ).Append( ")CK.Poco.Exc.JsonGen.Importer.ReadAny( ref r, rCtx );" );
                };
            }

            static CodeReader GetPocoReader( IPocoType type )
            {
                return ( w, v ) => w.Append( v ).Append( ".ReadJson( ref r, rCtx );" );
            }

            static CodeReader GetBasicTypeCodeReader( IPocoType type )
            {
                if( type.Type == typeof(int) )
                {
                    return (w,v) => w.Append( v ).Append( "=r.GetInt32();if(!r.Read())rCtx.ReadMoreData(ref r);" );
                }
                if( type.Type == typeof( bool ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "=r.GetBoolean();if(!r.Read())rCtx.ReadMoreData(ref r);" );
                }
                if( type.Type == typeof( string ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "=r.GetString();if(!r.Read())rCtx.ReadMoreData(ref r);" );
                }
                if( type.Type == typeof( double ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "=r.GetDouble();if(!r.Read())rCtx.ReadMoreData(ref r);" );
                }
                if( type.Type == typeof( float ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "=r.GetSingle();if(!r.Read())rCtx.ReadMoreData(ref r);" );
                }
                if( type.Type == typeof( byte ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "=r.GetByte();if(!r.Read())rCtx.ReadMoreData(ref r);" );
                }
                if( type.Type == typeof( sbyte ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "=r.GetSByte();if(!r.Read())rCtx.ReadMoreData(ref r);" );
                }
                if( type.Type == typeof( DateTime ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "=r.GetDateTime();if(!r.Read())rCtx.ReadMoreData(ref r);" );
                }
                if( type.Type == typeof( DateTimeOffset ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "=r.GetDateTimeOffset();if(!r.Read())rCtx.ReadMoreData(ref r);" );
                }
                if( type.Type == typeof( TimeSpan ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "=TimeSpan.FromTicks(r.TokenType==System.Text.Json.JsonTokenType.String?Int64.Parse(r.GetString(),System.Globalization.NumberFormatInfo.InvariantInfo):r.GetInt64()); if(!r.Read())rCtx.ReadMoreData(ref r);" );
                }
                if( type.Type == typeof( short ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "=r.GetInt16();if(!r.Read())rCtx.ReadMoreData(ref r);" );
                }
                if( type.Type == typeof( ushort ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "=r.GetUInt16();if(!r.Read())rCtx.ReadMoreData(ref r);" );
                }
                if( type.Type == typeof( BigInteger ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "=System.Numerics.BigInteger.Parse(r.GetString(),System.Globalization.NumberFormatInfo.InvariantInfo );if(!r.Read())rCtx.ReadMoreData(ref r);" );
                }
                if( type.Type == typeof( long ) )
                {
                    // Challenge the data itself (apply Postel's law, see https://en.wikipedia.org/wiki/Robustness_principle).
                    // WHY is the Utf8JsonReader has a perfect GetInt64WithQuotes() that is internal?
                    // Because Microsoft guys don't want you to be able to do the same as them... :(
                    return ( w, v ) => w.Append( v ).Append( "=r.TokenType==System.Text.Json.JsonTokenType.String?Int64.Parse(r.GetString(),System.Globalization.NumberFormatInfo.InvariantInfo):r.GetInt64();if(!r.Read())rCtx.ReadMoreData(ref r);" );
                }
                if( type.Type == typeof( ulong ) )
                {
                    // Challenge the data itself (apply Postel's law, see https://en.wikipedia.org/wiki/Robustness_principle).
                    // WHY is the Utf8JsonReader has a perfect GetUInt64WithQuotes() that is internal?
                    // Because Microsoft guys don't want you to be able to do the same as them... :(
                    return ( w, v ) => w.Append( v ).Append( "=r.TokenType==System.Text.Json.JsonTokenType.String?UInt64.Parse(r.GetString(),System.Globalization.NumberFormatInfo.InvariantInfo):r.GetUInt64();if(!r.Read())rCtx.ReadMoreData(ref r);" );
                }
                if( type.Type == typeof( Guid ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "=r.GetGuid();if(!r.Read())rCtx.ReadMoreData(ref r);" );
                }
                if( type.Type == typeof( uint ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "=r.GetUInt32();if(!r.Read())rCtx.ReadMoreData(ref r);" );
                }
                if( type.Type == typeof( decimal ) )
                {
                    // Challenge the data itself (apply Postel's law, see https://en.wikipedia.org/wiki/Robustness_principle).
                    // WHY is the Utf8JsonReader has a perfect  GetUInt64WithQuotes() that is internal?
                    // Because Microsoft guys don't want you to be able to do the same as them... :(
                    return ( w, v ) => w.Append( v ).Append( "=r.TokenType==System.Text.Json.JsonTokenType.String?Decimal.Parse(r.GetString(),System.Globalization.NumberFormatInfo.InvariantInfo ):r.GetDecimal();if(!r.Read())rCtx.ReadMoreData(ref r);" );
                }
                if( type.Type == typeof( NormalizedCultureInfo ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "= CK.Core.NormalizedCultureInfo.GetNormalizedCultureInfo( r.GetString() );if(!r.Read())rCtx.ReadMoreData(ref r);" );
                }
                if( type.Type == typeof( ExtendedCultureInfo ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "= CK.Core.ExtendedCultureInfo.GetExtendedCultureInfo( r.GetString() );if(!r.Read())rCtx.ReadMoreData(ref r);" );
                }
                if( type.Type == typeof( SimpleUserMessage ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "= CK.Core.GlobalizationJsonHelper.ReadSimpleUserMessageFromJsonArray( ref r, rCtx );" );
                }
                if( type.Type == typeof( UserMessage ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "= CK.Core.GlobalizationJsonHelper.ReadUserMessageFromJsonArray( ref r, rCtx );" );
                }
                if( type.Type == typeof( CodeString ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "= CK.Core.GlobalizationJsonHelper.ReadCodeStringFromJsonArray( ref r, rCtx );" );
                }
                if( type.Type == typeof( MCString ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "= CK.Core.GlobalizationJsonHelper.ReadMCStringFromJsonArray( ref r, rCtx );" );
                }
                if( type.Type == typeof( FormattedString ) )
                {
                    return ( w, v ) => w.Append( v ).Append( "= CK.Core.GlobalizationJsonHelper.ReadFormattedStringFromJsonArray( ref r, rCtx );" );
                }
                return Throw.NotSupportedException<CodeReader>( type.CSharpName );
            }

            CodeReader GetArrayCodeReader( ICollectionPocoType type )
            {
                var readerFunction = GetReadFunctionName( type.ItemTypes[0] );
                return ( writer, v ) => writer.Append( v ).Append( "=CK.Poco.Exc.JsonGen.Importer.ReadArray(ref r," )
                                              .Append( readerFunction )
                                              .Append( ",rCtx);" );
            }

            CodeReader GetListOrSetCodeReader( ICollectionPocoType type )
            {
                var readerFunction = GetReadFunctionName( type.ItemTypes[0] );
                return ( writer, v ) => writer.Append( "CK.Poco.Exc.JsonGen.Importer.FillListOrSet(ref r," )
                                              .Append( v )
                                              .Append( "," )
                                              .Append( readerFunction )
                                              .Append( ",rCtx);" );
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
                                                  .Append( ",rCtx);" );
                }
                var keyFunction = GetReadFunctionName( type.ItemTypes[0] );
                return ( writer, v ) => writer.Append( "CK.Poco.Exc.JsonGen.Importer.FillDictionary(ref r," )
                                              .Append( v )
                                              .Append( "," )
                                              .Append( keyFunction )
                                              .Append( "," )
                                              .Append( valueFunction )
                                              .Append( ",rCtx);" );
            }

            static CodeReader GetRecordCodeReader( IPocoType type )
            {
                Debug.Assert( type.Kind == PocoTypeKind.Record || type.Kind == PocoTypeKind.AnonymousRecord );
                return ( w, v ) => w.Append( "CK.Poco.Exc.JsonGen.Importer.Read_" )
                                    .Append( type.Index )
                                    .Append( "(ref r,ref " )
                                    .Append( v ).Append( ",rCtx);" );
            }

        }
    }
}
