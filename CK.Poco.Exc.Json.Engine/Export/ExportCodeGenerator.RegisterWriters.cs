using CK.CodeGen;
using CK.Core;
using System.Numerics;
using System;
using System.Diagnostics;
using static CK.Core.PocoJsonExportSupport;

namespace CK.Setup.PocoJson
{
    sealed partial class ExportCodeGenerator
    {
        // Step 1: The _writers array is filled with Writer delegates for all Exchangeable and NonNullable types
        //         Writers for the same "oblivious family" will share the same delegate.
        void RegisterWriters()
        {
            foreach( var type in _nameMap.ExchangeableNonNullableTypes )
            {
                switch( type.Kind )
                {
                    case PocoTypeKind.UnionType:
                    case PocoTypeKind.AbstractIPoco:
                    case PocoTypeKind.Any:
                        _writers[type.Index >> 1] = ObjectWriter;
                        break;
                    case PocoTypeKind.IPoco:
                        _writers[type.Index >> 1] = PocoWriter;
                        break;
                    case PocoTypeKind.Basic:
                        _writers[type.Index >> 1] = GetBasicTypeCodeWriter( type );
                        break;
                    case PocoTypeKind.Array:
                        {
                            var tA = (ICollectionPocoType)type;
                            _writers[type.Index >> 1] = tA.ItemTypes[0].Type == typeof( byte )
                                                            ? ( writer, v ) => writer.Append( "w.WriteBase64StringValue( " ).Append( v ).Append( " );" )
                                                            : GetCollectionObliviousCodeWriter( type );
                            break;
                        }
                    case PocoTypeKind.List:
                    case PocoTypeKind.HashSet:
                    case PocoTypeKind.Dictionary:
                        _writers[type.Index >> 1] = GetCollectionObliviousCodeWriter( type );
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

            static void PocoWriter( ICodeWriter writer, string variableName )
            {
                writer.Append( "((PocoJsonExportSupport.IWriter)" ).Append( variableName ).Append( ").WriteJson( w, wCtx );" );
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

            static CodeWriter GetCollectionObliviousCodeWriter( IPocoType type )
            {
                if( type.ImplTypeName != type.ObliviousType.ImplTypeName )
                {
                    // The type is an adapter that is a type.ObliviousType.ImplTypeName.
                    return ( writer, v ) => writer.Append( "CK.Poco.Exc.JsonGen.Exporter.Write_" )
                                                  .Append( type.ObliviousType.Index )
                                                  .Append( "( w, (" ).Append( type.ObliviousType.ImplTypeName ).Append( ")" )
                                                  .Append( v )
                                                  .Append( ",wCtx);" );
                }
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
