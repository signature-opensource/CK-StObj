using CK.CodeGen;
using CK.Core;
using System;
using System.Linq;
using System.Numerics;

namespace CK.Setup.PocoJson
{
    sealed partial class ExportCodeGenerator
    {
        sealed class WriterMap
        {
            readonly IPocoTypeNameMap _nameMap;
            readonly CodeWriter[] _writers;

            public WriterMap( IPocoTypeNameMap nameMap )
            {
                _nameMap = nameMap;
                _writers = new CodeWriter[nameMap.TypeSystem.AllNonNullableTypes.Count];
            }

            /// <summary>
            /// Straight call to the registered writer delegate.
            /// </summary>
            /// <param name="t">The type for which write code must be generated.</param>
            /// <param name="writer">The target writer.</param>
            /// <param name="variableName">The variable name that must be written.</param>
            public void RawWrite( IPocoType t, ICodeWriter writer, string variableName )
            {
                _writers[t.Index>>1].Invoke( writer, variableName );
            }

            /// <summary>
            /// Generates what is needed to write a <paramref name="variableName"/> of type <paramref name="t"/>,
            /// handling null. Reference types are always null checked and a null may be emitted to handle
            /// mapping to oblivious types. Only in some case where the <see cref="IPocoType.IsNullable"/> is false
            /// can the null check be skipped (and this will throw an exception instead of emitting a null if the data
            /// is actualy null):
            /// <list type="bullet">
            ///     <item>
            ///     <paramref name="trustNonNullableRef"/> is true for Poco and named record fields (as a Poco or a named
            ///     record is its own oblivious) but not for anonymous records fields since we handle the oblivious that
            ///     may differ from the actual type.
            ///     </item>
            ///     <item>
            ///     <paramref name="trustNonNullableRef"/> is also true for dictionary keys.
            ///     And since we handle the exact implementation type for dictionnaries, we also handle nullability values.
            ///     </item>
            /// </list>
            /// </summary>
            /// <param name="writer">The target writer.</param>
            /// <param name="t">The poco type.</param>
            /// <param name="variableName">The variable name.</param>
            /// <param name="trustNonNullableRef">True to trust a false <see cref="IPocoType.IsNullable"/> for reference type.</param>
            public void GenerateWrite( ICodeWriter writer, IPocoType t, string variableName, bool trustNonNullableRef = false )
            {
                if( t.Type.IsValueType )
                {
                    if( t.IsNullable )
                    {
                        writer.Append( "if( !" ).Append( variableName ).Append( ".HasValue ) w.WriteNullValue();" ).NewLine()
                              .Append( "else" )
                              .OpenBlock();
                        if( t is IRecordPocoType )
                        {
                            variableName = $"CommunityToolkit.HighPerformance.NullableExtensions.DangerousGetValueOrDefaultReference(ref {variableName})";
                        }
                        else
                        {
                            variableName = $"{variableName}.Value";
                        }
                        _writers[t.Index >> 1].Invoke( writer, variableName );
                        writer.CloseBlock();
                    }
                    else
                    {
                        _writers[t.Index >> 1].Invoke( writer, variableName );
                        writer.NewLine();
                    }
                }
                else
                {
                    // Since we are working in oblivious mode, any reference type MAY be null unless we trust it.
                    if( t.IsNullable ) trustNonNullableRef = false;
                    if( !trustNonNullableRef )
                    {
                        writer.Append( "if( " ).Append( variableName ).Append( " == null ) w.WriteNullValue();" ).NewLine()
                          .Append( "else" )
                          .OpenBlock();
                    }
                    _writers[t.Index >> 1].Invoke( writer, variableName );
                    if( !trustNonNullableRef ) writer.CloseBlock();
                }
            }

            public CodeWriter GetWriter( IPocoType t )
            {
                Throw.DebugAssert( _nameMap.TypeSet.Contains( t ) );
                var w = _writers[t.Index >> 1];
                if( w == null )
                {
                    var final = t.FinalType;
                    Throw.DebugAssert( t.IsPolymorphic || final != null );
                    if( t != final && final != null )
                    {
                        w = GetWriter( final );
                    }
                    else
                    {
                        switch( t.Kind )
                        {
                            case PocoTypeKind.UnionType:
                            case PocoTypeKind.AbstractPoco:
                            case PocoTypeKind.Any:
                                w = ObjectWriter;
                                break;
                            case PocoTypeKind.PrimaryPoco:
                            case PocoTypeKind.SecondaryPoco:
                                w = PocoWriter;
                                break;
                            case PocoTypeKind.Basic:
                                w = GetBasicTypeCodeWriter( t );
                                break;
                            case PocoTypeKind.Array:
                                {
                                    var type = (ICollectionPocoType)t;
                                    w = type.ItemTypes[0].Type == typeof( byte )
                                                                    ? ByteArrayWriter
                                                                    : GetEnumerableObliviousWriter( type );
                                    break;
                                }
                            case PocoTypeKind.List:
                            case PocoTypeKind.HashSet:
                                w = GetEnumerableObliviousWriter( (ICollectionPocoType)t );
                                break;
                            case PocoTypeKind.Dictionary:
                                w = GetDictionaryWriter( (ICollectionPocoType)t );
                                break;
                            case PocoTypeKind.Record:
                            case PocoTypeKind.AnonymousRecord:
                                w = GetRecordObliviousCodeWriter( t );
                                break;
                            case PocoTypeKind.Enum:
                                {
                                    var tE = (IEnumPocoType)t;
                                    w = ( writer, v ) => GenerateWrite( writer, tE.UnderlyingType, $"(({tE.UnderlyingType.CSharpName}){v})" );
                                    break;
                                }
                            default: throw new NotSupportedException( t.Kind.ToString() );
                        }
                    }
                    _writers[t.Index >> 1] = w;
                }
                return w;
            }

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

            static void EnumerableWriter( ICodeWriter writer, string variableName )
            {
                writer.Append( "CK.Poco.Exc.JsonGen.Exporter.WriteEnumerable( w, " ).Append( variableName ).Append( ", wCtx );" );
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

            CodeWriter GetRecordObliviousCodeWriter( IPocoType type )
            {
                Throw.DebugAssert( type.Kind is PocoTypeKind.Record or PocoTypeKind.AnonymousRecord );
                if( !type.IsOblivious ) return GetWriter( type.ObliviousType );
                return ( writer, v ) => writer.Append( "CK.Poco.Exc.JsonGen.Exporter.Write_" )
                                              .Append( type.Index )
                                              .Append( "( w, ref " )
                                              .Append( v ).Append( ", wCtx );" );
            }

            CodeWriter GetEnumerableObliviousWriter( ICollectionPocoType c )
            {
                Throw.DebugAssert( c.Kind is PocoTypeKind.Array or PocoTypeKind.List or PocoTypeKind.HashSet );
                var tI = c.ItemTypes[0];
                if( tI.Kind is PocoTypeKind.AbstractPoco )
                {
                    Throw.DebugAssert( "HashSet cannot contain IPoco (since IsHashSafe is false).", c.Kind is PocoTypeKind.Array or PocoTypeKind.List );
                    return EnumerablePocoWriterWithType;
                }
                if( tI.Kind is PocoTypeKind.PrimaryPoco or PocoTypeKind.SecondaryPoco )
                {
                    Throw.DebugAssert( "HashSet cannot contain IPoco (since IsHashSafe is false).", c.Kind is PocoTypeKind.Array or PocoTypeKind.List );
                    return EnumerablePocoWriter;
                }
                if( tI.IsPolymorphic )
                {
                    // HashSet can contain polymorhic items like basic reference type (Extended/NormalizedCultureInfo).",
                    return EnumerableWriter;
                }
                // HashSet<TItem> is not natively covariant. An adapter is generated for
                // value types, string and other basic reference types.
                //
                // The good news is that the adapters for T (value types, string and other basic reference types) are
                // specialized HashSet<T> because for these types ImplTypeName == CSharpName.
                // And HashSet<T> is the oblivious: we can use the oblivious type.
                // We factorize to the Oblivious type but we don't project to the item's type
                // because we want array to use AsSpan(), list to to use CollectionsMarshal.AsSpan
                // and hashset to use the IEnumerable.
                //
                if( !c.IsOblivious ) return GetWriter( c.ObliviousType );
                return ( writer, v ) => writer.Append( "CK.Poco.Exc.JsonGen.Exporter.Write_" )
                                              .Append( c.Index )
                                              .Append( "(w," ).Append( v ).Append( ", wCtx);" );
            }

            static CodeWriter GetDictionaryWriter( ICollectionPocoType c )
            {
                Throw.DebugAssert( c.Kind is PocoTypeKind.Dictionary );
                // IReadOnlyDictionary<TKey,TValue> is NOT covariant on TValue.
                // When TValue is polymorphic we cannot use a IReadOnlyDictionary parameter
                // in a simple "generic" helper.
                // One solution could be to use both IEnumerable<TKey> Keys and IEnumerable<TValue> Values parameters
                // but we prefer to handle only one parameter and we also want to avoid the projection of the values
                // by the adapters.
                //
                // It is not easy to split the complexity here as we did for the list and set above.
                // We (unfortunately) generate a writer for each dictionary implementation type.
                //
                // The only good thing is that since we are bound to the exact type, we can optimize the write of
                // nullable vs. non nullable reference type values.

                return ( writer, v ) => writer.Append( "CK.Poco.Exc.JsonGen.Exporter.Write_" )
                              .Append( c.Index )
                              .Append( "(w," ).Append( v ).Append( ", wCtx);" );
            }

        }

        // Step 1: The writer map is filled with Writer delegates for all serializable types.
        void InitializeWriterMap()
        {
            // This is not required (writers are initialized on demand) but
            // this avoids the null test each time a writer is needed.
            foreach( var type in _nameMap.TypeSet.NonNullableTypes )
            {
                _writerMap.GetWriter( type );
            }

            _exporterType.GeneratedByComment()
                         .Append(
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

            internal static void WriteEnumerable( System.Text.Json.Utf8JsonWriter w, IEnumerable<object> v, CK.Poco.Exc.Json.PocoJsonWriteContext wCtx )
            {
               w.WriteStartArray();
               foreach( var e in v )
               {
                  if( e == null ) w.WriteNullValue();
                  else CK.Poco.Exc.JsonGen.Exporter.WriteAny( w, e, wCtx );
               }
               w.WriteEndArray();
            }
            """ );
        }
    }
}
