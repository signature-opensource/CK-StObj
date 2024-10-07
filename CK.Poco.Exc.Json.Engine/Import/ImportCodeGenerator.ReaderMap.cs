using CK.CodeGen;
using CK.Core;
using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;

namespace CK.Setup.PocoJson;

sealed partial class ImportCodeGenerator
{
    sealed class ReaderMap
    {
        readonly CodeReader[] _readers;
        readonly IPocoTypeNameMap _nameMap;

        public ReaderMap( IPocoTypeNameMap nameMap )
        {
            _nameMap = nameMap;
            _readers = new CodeReader[nameMap.TypeSystem.AllNonNullableTypes.Count];
        }

        public IPocoTypeNameMap NameMap => _nameMap;

        public void Initialize( ReaderFunctionMap functionMap )
        {
            foreach( var type in _nameMap.TypeSet.NonNullableTypes )
            {
                GetReader( type, functionMap );
            }
        }

        public void GenerateRead( ICodeWriter writer, IPocoType t, string variableName, bool requiresInit )
        {
            if( t.IsNullable )
            {
                writer.Append( "if(r.TokenType==System.Text.Json.JsonTokenType.Null)" )
                        .OpenBlock()
                        .Append( variableName ).Append( "=default;" ).NewLine()
                        .Append( "if(!r.Read()) rCtx.ReadMoreData(ref r);" )
                        .CloseBlock()
                        .Append( "else" )
                        .OpenBlock();
                DoGenerateRead( _readers, writer, t, variableName, requiresInit );
                writer.CloseBlock();
            }
            else
            {
                DoGenerateRead( _readers, writer, t, variableName, requiresInit );
            }

            static string? GetInitSource( IPocoType t )
            {
                // BasicTypes will be assigned from low-level reader functions.
                // Enum are read by casting the underlying type.
                if( t.Kind == PocoTypeKind.Basic || t.Kind == PocoTypeKind.Enum ) return null;
                // If the type has a default value source, use it.
                var def = t.DefaultValueInfo;
                if( def.RequiresInit ) return def.DefaultValue.ValueCSharpSource;
                // If the type is a struct it will be read by ref: the variable needs to be assigned
                // before ref can be used.
                if( t.Type.IsValueType ) return "default";
                // Reference types should have a DefaultValue.
                return null;
            }

            static void DoGenerateRead( CodeReader[] readers, ICodeWriter writer, IPocoType t, string variableName, bool requiresInit )
            {
                if( requiresInit )
                {
                    var init = GetInitSource( t.NonNullable );
                    if( init != null )
                    {
                        writer.Append( variableName ).Append( "=" ).Append( init ).Append( ";" ).NewLine();
                    }
                }
                // For nullable records, we need this adapter.
                // This is crappy and inefficient.
                // This is because even if we can get the reference to the Nullable value field to fill it,
                // we miss the capability to set its HasValue to true. So we recopy the read value as the
                // value (thanks to GetValueOrDefault that doesn't check the HasValue and returns the value as-is).
                string? originName = null;
                if( t.IsNullable && (t.Kind == PocoTypeKind.AnonymousRecord || t.Kind == PocoTypeKind.Record) )
                {
                    originName = variableName;
                    variableName = $"CommunityToolkit.HighPerformance.NullableExtensions.DangerousGetValueOrDefaultReference( ref {variableName} )";
                }
                readers[t.Index >> 1].Invoke( writer, variableName );
                if( originName != null )
                {
                    writer.NewLine().Append( originName ).Append( " = " ).Append( originName ).Append( ".GetValueOrDefault();" );
                }
            }
        }

        CodeReader GetReader( IPocoType type, ReaderFunctionMap functionMap )
        {
            Throw.DebugAssert( _nameMap.TypeSet.Contains( type ) && !type.IsNullable );
            var r = _readers[type.Index >> 1];
            if( r == null )
            {
                switch( type.Kind )
                {
                    case PocoTypeKind.UnionType:
                    case PocoTypeKind.Any:
                        r = ObjectReader;
                        break;
                    case PocoTypeKind.AbstractPoco:
                        r = GetAbstractPocoReader( type );
                        break;
                    case PocoTypeKind.SecondaryPoco:
                    case PocoTypeKind.PrimaryPoco:
                    {
                        r = GetPocoReader( type );
                        break;
                    }
                    case PocoTypeKind.Basic:
                        r = GetBasicTypeCodeReader( type );
                        break;
                    case PocoTypeKind.Array:
                    {
                        var tA = (ICollectionPocoType)type;
                        r = tA.ItemTypes[0].Type == typeof( byte )
                                   ? ( w, v ) => w.Append( v ).Append( "=r.GetBytesFromBase64();if(!r.Read())rCtx.ReadMoreData(ref r);" )
                                   : GetArrayCodeReader( tA, functionMap );
                        break;
                    }
                    case PocoTypeKind.List:
                    case PocoTypeKind.HashSet:
                        r = GetListOrSetCodeReader( (ICollectionPocoType)type, functionMap );
                        break;
                    case PocoTypeKind.Dictionary:
                        r = GetDictionaryCodeReader( (ICollectionPocoType)type, functionMap );
                        break;
                    case PocoTypeKind.Record:
                    case PocoTypeKind.AnonymousRecord:
                        Throw.DebugAssert( "Record always have a regular type.", type.RegularType != null );
                        r = type.IsRegular ? GetRecordCodeReader( type ) : GetReader( type.RegularType, functionMap );
                        break;
                    case PocoTypeKind.Enum:
                    {
                        var tE = (IEnumPocoType)type;
                        r = ( w, v ) =>
                        {
                            w.OpenBlock()
                             .Append( "var " );
                            GenerateRead( w, tE.UnderlyingType, "u", false );
                            w.NewLine().Append( v ).Append( "=(" ).Append( tE.CSharpName ).Append( ")u;" )
                             .CloseBlock();
                        };
                        break;
                    }
                    default: throw new NotSupportedException( type.Kind.ToString() );
                }
                _readers[type.Index >> 1] = r;
            }
            return r;
        }

        static void ObjectReader( ICodeWriter writer, string variableName )
        {
            writer.Append( variableName ).Append( "=CK.Poco.Exc.JsonGen.Importer.ReadAny( ref r, rCtx );" );
        }

        static CodeReader GetPocoReader( IPocoType type )
        {
            return ( w, v ) => w.Append( "System.Runtime.CompilerServices.Unsafe.As<" ).Append( type.ImplTypeName ).Append( ">(" ).Append( v ).Append( ").ReadJson( ref r, rCtx );" );
        }

        static CodeReader GetAbstractPocoReader( IPocoType type )
        {
            Throw.DebugAssert( !type.IsNullable );
            return ( w, v ) =>
            {
                w.Append( v ).Append( "=(" ).Append( type.CSharpName ).Append( ")CK.Poco.Exc.JsonGen.Importer.ReadAny( ref r, rCtx );" );
            };
        }

        static CodeReader GetBasicTypeCodeReader( IPocoType type )
        {
            if( type.Type == typeof( int ) )
            {
                return ( w, v ) => w.Append( v ).Append( "=r.GetInt32();if(!r.Read())rCtx.ReadMoreData(ref r);" );
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
                return ( w, v ) => w.Append( v ).Append( "= CK.Core.GlobalizationJsonHelper.ResolveCulture( r.GetString(), rCtx ).PrimaryCulture;" ).NewLine()
                                                .Append( "if(!r.Read())rCtx.ReadMoreData(ref r);" );
            }
            if( type.Type == typeof( ExtendedCultureInfo ) )
            {
                return ( w, v ) => w.Append( v ).Append( "= CK.Core.GlobalizationJsonHelper.ResolveCulture( r.GetString(), rCtx );" ).NewLine()
                                                .Append( "if(!r.Read())rCtx.ReadMoreData(ref r);" );
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

        static CodeReader GetArrayCodeReader( ICollectionPocoType type, ReaderFunctionMap functionMap )
        {
            IPocoType tI = type.ItemTypes[0];
            if( tI.IsPolymorphic )
            {
                return ( writer, v ) => writer.Append( v )
                                              .Append( "=CK.Poco.Exc.JsonGen.Importer.ReadArrayOfAny<" )
                                              .Append( tI.ImplTypeName )
                                              .Append( ">(ref r, rCtx);" );
            }
            var readerFunction = functionMap.GetReadFunctionName( tI );
            return ( writer, v ) => writer.Append( v ).Append( "=CK.Poco.Exc.JsonGen.Importer.ReadArray(ref r," )
                                          .Append( readerFunction )
                                          .Append( ",rCtx);" );
        }

        CodeReader GetListOrSetCodeReader( ICollectionPocoType type, ReaderFunctionMap functionMap )
        {
            IPocoType tI = type.ItemTypes[0];
            if( tI.IsPolymorphic )
            {
                return ( writer, v ) => writer.Append( "CK.Poco.Exc.JsonGen.Importer.FillListOrSetOfAny(ref r," )
                                              .Append( v )
                                              .Append( ",rCtx);" );
            }
            var readerFunction = functionMap.GetReadFunctionName( tI );
            return ( writer, v ) => writer.Append( "CK.Poco.Exc.JsonGen.Importer.FillListOrSet(ref r," )
                                          .Append( v )
                                          .Append( "," )
                                          .Append( readerFunction )
                                          .Append( ",rCtx);" );
        }

        CodeReader GetDictionaryCodeReader( ICollectionPocoType type, ReaderFunctionMap functionMap )
        {
            var keyType = type.ItemTypes[0];
            var valueFunction = functionMap.GetReadFunctionName( type.ItemTypes[1] );
            if( keyType.Type == typeof( string ) )
            {
                return ( writer, v ) => writer.Append( "CK.Poco.Exc.JsonGen.Importer.FillDynamicObject(ref r," )
                                              .Append( v )
                                              .Append( "," )
                                              .Append( valueFunction )
                                              .Append( ",rCtx);" );
            }
            var keyFunction = functionMap.GetReadFunctionName( type.ItemTypes[0] );
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
