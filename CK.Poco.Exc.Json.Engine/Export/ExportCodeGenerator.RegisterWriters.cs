using CK.CodeGen;
using CK.Core;
using System.Numerics;
using System;
using System.Diagnostics;
using static CK.Core.PocoJsonExportSupport;
using System.Reflection.Metadata;

namespace CK.Setup.PocoJson
{
    sealed partial class ExportCodeGenerator
    {
        // Step 1: The _writers array is filled with Writer delegates for all Exchangeable and NonNullable types
        //         Writers for the same "oblivious family" will share the same delegate.
        void RegisterWriters()
        {
            _exporterType.GeneratedByComment().Append(
            """
            internal static void WriteEnumerablePoco( System.Text.Json.Utf8JsonWriter w, IEnumerable<IPoco> v, CK.Poco.Exc.Json.PocoJsonWriteContext wCtx )
            {
               w.WriteStartArray();
               foreach( var e in v )
               {
                  if( e == null ) w.WriteNullValue();
                  else System.Runtime.CompilerServices.Unsafe.As<PocoJsonExportSupport.IWriter>( e ).WriteJson( w, wCtx );
               }
               w.WriteEndArray();
            }

            internal static void WriteEnumerablePocoWithType( System.Text.Json.Utf8JsonWriter w, IEnumerable<IPoco> v, CK.Poco.Exc.Json.PocoJsonWriteContext wCtx )
            {
               w.WriteStartArray();
               foreach( var e in v )
               {
                  if( e == null ) w.WriteNullValue();
                  else System.Runtime.CompilerServices.Unsafe.As<PocoJsonExportSupport.IWriter>( e ).WriteJson( w, wCtx, true );
               }
               w.WriteEndArray();
            }
            """ );

            foreach( var type in _nameMap.TypeSet.NonNullableTypes )
            {
                switch( type.Kind )
                {
                    case PocoTypeKind.UnionType:
                    case PocoTypeKind.AbstractPoco:
                    case PocoTypeKind.Any:
                        _writers[type.Index >> 1] = ObjectWriter;
                        break;
                    case PocoTypeKind.PrimaryPoco:
                    case PocoTypeKind.SecondaryPoco:
                        _writers[type.Index >> 1] = PocoWriter;
                        break;
                    case PocoTypeKind.Basic:
                        _writers[type.Index >> 1] = GetBasicTypeCodeWriter( type );
                        break;
                    case PocoTypeKind.Array:
                        {
                            var t = (ICollectionPocoType)type;
                            _writers[type.Index >> 1] = t.ItemTypes[0].Type == typeof( byte )
                                                            ? ByteArrayWriter
                                                            : t.ItemTypes[0].Kind switch
                                                            {
                                                                PocoTypeKind.AbstractPoco => EnumerablePocoWriterWithType,
                                                                PocoTypeKind.PrimaryPoco or PocoTypeKind.SecondaryPoco => EnumerablePocoWriter,
                                                                _ => GetCollectionWriter( type )
                                                            };
                            break;
                        }
                    case PocoTypeKind.List:
                    case PocoTypeKind.HashSet:
                        {
                            _writers[type.Index >> 1] = ((ICollectionPocoType)type).ItemTypes[0].Kind switch
                            {
                                PocoTypeKind.AbstractPoco => EnumerablePocoWriterWithType,
                                PocoTypeKind.PrimaryPoco or PocoTypeKind.SecondaryPoco => EnumerablePocoWriter,
                                _ => GetCollectionWriter( type )
                            };
                            break;
                        }
                    case PocoTypeKind.Dictionary:
                        _writers[type.Index >> 1] = GetCollectionWriter( type );
                        break;
                    case PocoTypeKind.Record:
                    case PocoTypeKind.AnonymousRecord:
                        _writers[type.Index >> 1] = GetRecordObliviousCodeWriter( type );
                        break;
                    case PocoTypeKind.Enum:
                        {
                            var tE = (IEnumPocoType)type;
                            _writers[type.Index >> 1] = ( writer, v ) => GenerateWrite( writer, tE.UnderlyingType, $"(({tE.UnderlyingType.CSharpName}){v})" );
                            break;
                        }
                }
            }
            return;

            static void ObjectWriter( ICodeWriter writer, string variableName )
            {
                writer.Append( "CK.Poco.Exc.JsonGen.Exporter.WriteAny( w, " ).Append( variableName ).Append( ", wCtx );" );
            }

            static void ByteArrayWriter( ICodeWriter writer, string variableName )
            {
                writer.Append( "w.WriteBase64StringValue( " ).Append( variableName ).Append( " );" );
            }

            static void PocoWriter( ICodeWriter writer, string variableName )
            {
                writer.Append( "((PocoJsonExportSupport.IWriter)" ).Append( variableName ).Append( ").WriteJson( w, wCtx );" );
            }

            static void EnumerablePocoWriterWithType( ICodeWriter writer, string variableName )
            {
                writer.Append( "CK.Poco.Exc.JsonGen.Exporter.WriteEnumerablePocoWithType( w, " ).Append( variableName ).Append( ", wCtx );" );
            }

            static void EnumerablePocoWriter( ICodeWriter writer, string variableName )
            {
                writer.Append( "CK.Poco.Exc.JsonGen.Exporter.WriteEnumerablePoco( w, " ).Append( variableName ).Append( ", wCtx );" );
            }

            static CodeWriter GetBasicTypeCodeWriter( IPocoType type )
            {
                if( type.Type == typeof( int )
                    || type.Type == typeof( uint )
                    || type.Type == typeof( short )
                    || type.Type == typeof( ushort )
                    || type.Type == typeof( byte )
                    || type.Type == typeof( sbyte )
                    || type.Type == typeof( double )
                    || type.Type == typeof( float ) )
                {
                    return NumberWriter;
                }
                if( type.Type == typeof( string )
                         || type.Type == typeof( Guid )
                         || type.Type == typeof( DateTime )
                         || type.Type == typeof( DateTimeOffset ) )
                {
                    return StringWriter;
                }
                else if( type.Type == typeof( bool ) )
                {
                    return ( writer, v ) => writer.Append( "w.WriteBooleanValue( " ).Append( v ).Append( " );" );
                }
                else if( type.Type == typeof( decimal )
                         || type.Type == typeof( long )
                         || type.Type == typeof( ulong ) )
                {
                    return NumberAsStringWriter;
                }
                else if( type.Type == typeof( BigInteger ) )
                {
                    // Use the BigInteger.ToString(String) method with the "R" format specifier to generate the string representation of the BigInteger value.
                    // Otherwise, the string representation of the BigInteger preserves only the 50 most significant digits of the original value, and data may
                    // be lost when you use the Parse method to restore the BigInteger value.
                    return ( writer, v ) => writer.Append( "w.WriteStringValue( " )
                                                  .Append( v )
                                                  .Append( ".ToString( \"R\", System.Globalization.NumberFormatInfo.InvariantInfo ) );" );
                }
                else if( type.Type == typeof( TimeSpan ) )
                {
                    return ( writer, v ) => writer.Append( "w.WriteStringValue( " )
                                                  .Append( v )
                                                  .Append( ".Ticks.ToString( System.Globalization.NumberFormatInfo.InvariantInfo ) );" );
                }
                else if( type.Type == typeof( SimpleUserMessage )
                         || type.Type == typeof( UserMessage )
                         || type.Type == typeof( FormattedString )
                         || type.Type == typeof( MCString )
                         || type.Type == typeof( CodeString ) )
                {
                    return GlobalizationTypesWriter;
                }
                else if( type.Type == typeof( NormalizedCultureInfo )
                         || type.Type == typeof( ExtendedCultureInfo ) )
                {
                    return CultureInfoWriter;
                }
                return Throw.NotSupportedException<CodeWriter>( type.Type.ToCSharpName() );

                static void NumberWriter( ICodeWriter writer, string variableName )
                {
                    writer.Append( "w.WriteNumberValue( " ).Append( variableName ).Append( " );" );
                }

                static void StringWriter( ICodeWriter write, string variableName )
                {
                    write.Append( "w.WriteStringValue( " ).Append( variableName ).Append( " );" );
                }

                static void CultureInfoWriter( ICodeWriter write, string variableName )
                {
                    write.Append( "w.WriteStringValue( " ).Append( variableName ).Append( ".Name );" );
                }

                static void GlobalizationTypesWriter( ICodeWriter write, string variableName )
                {
                    write.Append( "CK.Core.GlobalizationJsonHelper.WriteAsJsonArray( w, " )
                                                                    .Append( variableName )
                                                                    .Append( " );" );
                }

                static void NumberAsStringWriter( ICodeWriter write, string variableName )
                {
                    write.Append( "w.WriteStringValue( " ).Append( variableName ).Append( ".ToString( System.Globalization.NumberFormatInfo.InvariantInfo ) );" );
                }
            }

            static CodeWriter GetCollectionWriter( IPocoType type )
            {
                return ( writer, v ) => writer.Append( "CK.Poco.Exc.JsonGen.Exporter.Write_" )
                                              .Append( type.ObliviousType.Index )
                                              .Append( "(w," )
                                              .Append( v ).Append( ",wCtx);" );
            }

            static CodeWriter GetRecordObliviousCodeWriter( IPocoType type )
            {
                Debug.Assert( type.Kind == PocoTypeKind.Record || type.Kind == PocoTypeKind.AnonymousRecord );
                return ( writer, v ) => writer.Append( "CK.Poco.Exc.JsonGen.Exporter.Write_" )
                                              .Append( type.ObliviousType.Index )
                                              .Append( "( w, ref " )
                                              .Append( v ).Append( ", wCtx );" );
            }

        }
    }
}
