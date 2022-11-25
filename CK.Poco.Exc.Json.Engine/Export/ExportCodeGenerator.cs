using CK.CodeGen;
using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json.Serialization.Metadata;
using static CK.Core.PocoJsonExportSupport;

namespace CK.Setup.PocoJson
{
    /// <summary>
    /// The code writer delegate is in charge of generating the write code into a <see cref="System.Text.Json.Utf8JsonWriter"/>
    /// from a variable named "w" and a PocoJsonExportOptions variable named "options".
    /// </summary>
    /// <param name="write">The code writer to uses.</param>
    /// <param name="variableName">The variable name to write.</param>
    delegate void CodeWriter( ICodeWriter write, string variableName );

    class ExportCodeGenerator
    {
        readonly ITypeScope _pocoDirectory;
        readonly ExchangeableTypeNameMap _nameMap;
        readonly ICSCodeGenerationContext _generationContext;
        readonly CodeWriter[] _writers;

        public ExportCodeGenerator( ITypeScope pocoDirectory, ExchangeableTypeNameMap nameMap, ICSCodeGenerationContext generationContext )
        {
            _pocoDirectory = pocoDirectory;
            _nameMap = nameMap;
            _generationContext = generationContext;
            _writers = new CodeWriter[nameMap.TypeSystem.AllNonNullableTypes.Count];
        }

        void GenerateWrite( ICodeWriter writer, IPocoType t, string variableName )
        {
            if( t.IsNullable ) GenerateNullableWrite( writer, t, variableName );
            else GenerateNonNullableWrite( writer, t, variableName );

            void GenerateNonNullableWrite( ICodeWriter writer, IPocoType nonNullable, string variableName )
            {
                Debug.Assert( _writers[nonNullable.Index >> 1] != null );
                Debug.Assert( !nonNullable.IsNullable && (nonNullable.Index & 1) == 0 );
                if( !nonNullable.Type.IsValueType )
                {
                    writer.Append( "if( " ).Append( variableName )
                          .Append( " == null ) w.ThrowJsonNullWriteException();" )
                          .NewLine();
                }
                _writers[nonNullable.Index >> 1].Invoke( writer, variableName );
            }

            void GenerateNullableWrite( ICodeWriter writer, IPocoType nullable, string variableName )
            {
                Debug.Assert( nullable.IsNullable && (nullable.Index & 1) == 1 );
                writer.Append( "if( " ).Append( variableName ).Append( " == null ) w.WriteNullValue();" ).NewLine()
                      .Append( "else" )
                      .OpenBlock();
                var v = nullable.Type.IsValueType
                        ? $"CommunityToolkit.HighPerformance.Extensions.NullableExtensions.DangerousGetValueOrDefaultReference(ref {variableName})"
                        : variableName;
                _writers[nullable.Index >> 1].Invoke( writer, v );
                writer.CloseBlock();
            }
        }

        void GenerateWritePropertyName( ICodeWriter writer, string name )
        {
            writer.Append( "w.WritePropertyName( options.UseCamelCase ? " )
                  .AppendSourceString( System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName( name ) )
                  .Append( " : " )
                  .AppendSourceString( name )
                  .Append( " );" ).NewLine();
        }

        public bool Run( IActivityMonitor monitor )
        {
            using( monitor.OpenInfo( $"Generating {_nameMap.ExchangeableNonNullableTypes.Count} Json code writers." ) )
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
                            _writers[type.Index >> 1] = GenerateBasicTypeWriter( type );
                            break;
                        case PocoTypeKind.List:
                        case PocoTypeKind.Array:
                        case PocoTypeKind.HashSet:
                        case PocoTypeKind.Dictionary:
                        case PocoTypeKind.Record:
                        case PocoTypeKind.AnonymousRecord:
                            _writers[type.Index >> 1] = ( writer, v ) => writer.Append( "PocoDirectory_CK.WriteJson_" )
                                                                               .Append( type.ImplNominalType.Index )
                                                                               .Append( "( w, ref " )
                                                                               .Append( v ).Append( ", options );" );
                            break;
                        case PocoTypeKind.Enum:
                            {
                                var tE = (IEnumPocoType)type;
                                _writers[type.Index >> 1] = ( writer, v ) => GenerateWrite( writer, tE.UnderlyingType, $"(({tE.UnderlyingType.CSharpName}){v})" );
                                break;
                            }
                    }
                }
            }
            foreach( var type in _nameMap.ExchangeableNonNullableTypes )
            {
                switch( type.Kind )
                {
                    case PocoTypeKind.IPoco:
                        GeneratePocoWriteMethod( monitor, (IPrimaryPocoType)type );
                        break;
                    case PocoTypeKind.AnonymousRecord:
                        GenerateAnonymousRecordWriteMethod( monitor, (IRecordPocoType)type );
                        break;
                    case PocoTypeKind.Record:
                        GenerateNamedRecordWriteMethod( monitor, (IRecordPocoType)type );
                        break;
                    case PocoTypeKind.List:
                    case PocoTypeKind.Array:
                        GenerateListOrArrayWriteMethod( monitor, (ICollectionPocoType)type );
                        break;
                    case PocoTypeKind.HashSet:
                        GenerateHashSetWriteMethod( monitor, (ICollectionPocoType)type );
                        break;
                    case PocoTypeKind.Dictionary:
                        GenerateDictionaryWriteMethod( monitor, (ICollectionPocoType)type );
                        break;
                }
            }
            GenerateWriteAny();
            return true;

            static void ObjectWriter( ICodeWriter writer, string variableName )
            {
                writer.Append( "PocoDirectory_CK.WriteAnyJson( w, " ).Append( variableName ).Append( ", options );" );
            }

            static void PocoWriter( ICodeWriter writer, string variableName )
            {
                writer.Append( "((PocoJsonExportSupport.IWriter)" ).Append( variableName ).Append( ").WriteJson( w, options );" );
            }

            static CodeWriter GenerateBasicTypeWriter( IPocoType type )
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
                return Throw.NotSupportedException<CodeWriter>( type.Type.ToCSharpName() );

                static void NumberWriter( ICodeWriter writer, string variableName )
                {
                    writer.Append( "w.WriteNumberValue( " ).Append( variableName ).Append( " );" );
                }

                static void StringWriter( ICodeWriter write, string variableName )
                {
                    write.Append( "w.WriteStringValue( " ).Append( variableName ).Append( " );" );
                }

                static void NumberAsStringWriter( ICodeWriter write, string variableName )
                {
                    write.Append( "w.WriteStringValue( " ).Append( variableName ).Append( ".ToString( System.Globalization.NumberFormatInfo.InvariantInfo ) );" );
                }
            }
        }

        void GenerateWriteJsonMethodHeader( IActivityMonitor monitor, IPocoType type )
        {
            _pocoDirectory.Append( "internal static void WriteJson_" )
                          .Append( type.Index )
                          .Append( "( System.Text.Json.Utf8JsonWriter w, ref " )
                          .Append( type.CSharpName ).Append( " v, CK.Poco.Exc.Json.Export.PocoJsonExportOptions options )" );
        }

        void GenerateDictionaryWriteMethod( IActivityMonitor monitor, ICollectionPocoType type )
        {
            GenerateWriteJsonMethodHeader( monitor, type );
            _pocoDirectory.OpenBlock();
            if( type.ItemTypes[0].Type == typeof(string) )
            {
                _pocoDirectory.Append( "w.WriteStartObject();" ).NewLine()
                              .Append( "foreach( var item in v )" )
                              .OpenBlock()
                              .Append( "w.WritePropertyName( item.Key );").NewLine()
                              .Append( "var vLoc = item.Value;" ).NewLine()
                              .Append( writer => GenerateWrite( writer, type.ItemTypes[1], "vLoc" ) ).NewLine()
                              .CloseBlock()
                              .Append( "w.WriteEndObject();" );
            }
            else
            {
                _pocoDirectory.Append( "w.WriteStartArray();" ).NewLine()
                              .Append( "foreach( var (k,e) in v )" )
                              .OpenBlock()
                              .Append( "w.WriteStartArray();" ).NewLine()
                              .Append( "var tK = k;" ).NewLine()
                              .Append( writer => GenerateWrite( writer, type.ItemTypes[0], "tK" ) ).NewLine()
                              .Append( "var tE = e;" ).NewLine()
                              .Append( writer => GenerateWrite( writer, type.ItemTypes[1], "tE" ) ).NewLine()
                              .Append( "w.WriteEndArray();" ).NewLine()
                              .CloseBlock()
                              .Append( "w.WriteEndArray();" );
            }
            _pocoDirectory.CloseBlock();
        }

        void GenerateHashSetWriteMethod( IActivityMonitor monitor, ICollectionPocoType type )
        {
            GenerateWriteJsonMethodHeader( monitor, type );
            _pocoDirectory.OpenBlock();
            _pocoDirectory.Append( "w.WriteStartArray();" ).NewLine()
                          .Append( "foreach( var item in v )" )
                          .OpenBlock()
                          .Append( "var loc = item;" ).NewLine()
                          .Append( writer => GenerateWrite( writer, type.ItemTypes[0], "loc" ) )
                          .CloseBlock()
                          .Append( "w.WriteEndArray();" ).NewLine()
                          .CloseBlock();
        }

        void GenerateListOrArrayWriteMethod( IActivityMonitor monitor, ICollectionPocoType type )
        {
            GenerateWriteJsonMethodHeader( monitor, type );
            _pocoDirectory.OpenBlock();
            if( type.Kind == PocoTypeKind.Array )
            {
                _pocoDirectory.Append( "var a = v.AsSpan();" ).NewLine();
            }
            else
            {
                _pocoDirectory.Append( "var a = System.Runtime.InteropServices.CollectionsMarshal.AsSpan( v );" ).NewLine();
            }
            _pocoDirectory.Append( "w.WriteStartArray();" ).NewLine()
                          .Append( "for( int i = 0; i < a.Length; ++i )" )
                          .OpenBlock()
                          .Append( writer => GenerateWrite( writer, type.ItemTypes[0], "a[i]" ) )
                          .CloseBlock()
                          .Append( "w.WriteEndArray();" ).NewLine()
                          .CloseBlock();
        }

        void GenerateAnonymousRecordWriteMethod( IActivityMonitor monitor, IRecordPocoType type )
        {
            GenerateWriteJsonMethodHeader( monitor, type );
            _pocoDirectory.OpenBlock()
                          .Append( "w.WriteStartArray();" ).NewLine();
            foreach( var f in type.Fields )
            {
                GenerateWrite( _pocoDirectory, f.Type, $"v.{f.Name}" );
            }
            _pocoDirectory.Append( "w.WriteEndArray();" ).NewLine()
                          .CloseBlock();
        }

        void GenerateNamedRecordWriteMethod( IActivityMonitor monitor, IRecordPocoType type )
        {
            GenerateWriteJsonMethodHeader( monitor, type );
            _pocoDirectory.OpenBlock()
                          .Append( "w.WriteStartObject();" ).NewLine();
            foreach( var f in type.Fields )
            {
                GenerateWritePropertyName( _pocoDirectory, f.Name );
                GenerateWrite( _pocoDirectory, f.Type, $"v.{f.Name}" );
            }
            _pocoDirectory.Append( "w.WriteEndObject();" ).NewLine()
                          .CloseBlock();
        }

        void GeneratePocoWriteMethod( IActivityMonitor monitor, IPrimaryPocoType type )
        {
            // Each Poco class is a PocoJsonExportSupport.IWriter.
            var pocoClass = _generationContext.Assembly.FindOrCreateAutoImplementedClass( monitor, type.FamilyInfo.PocoClass );
            pocoClass.Definition.BaseTypes.Add( new ExtendedTypeName( "PocoJsonExportSupport.IWriter" ) );

            // The Write method.
            // The write part will be filled with the properties (name and writer code).
            pocoClass.Append( "public void WriteJson( System.Text.Json.Utf8JsonWriter w, bool withType, CK.Poco.Exc.Json.Export.PocoJsonExportOptions options )" )
                     .OpenBlock()
                     .GeneratedByComment().NewLine()
                     .Append( "if( withType )" )
                     .OpenBlock()
                     .Append( "w.WriteStartArray();" ).NewLine()
                     .Append( writer => GenerateTypeHeader( writer, type ) )
                     .CloseBlock()
                     .Append( "WriteJson( w, options );" ).NewLine()
                     .Append( "if( withType )" )
                     .OpenBlock()
                     .Append( "w.WriteEndArray();" ).NewLine()
                     .CloseBlock()
                     .CloseBlock();

            pocoClass.Append( "public void WriteJson( System.Text.Json.Utf8JsonWriter w, CK.Poco.Exc.Json.Export.PocoJsonExportOptions options )" )
                     .OpenBlock()
                     .GeneratedByComment().NewLine()
                     .Append( "w.WriteStartObject();" ).NewLine()
                     .Append( "options ??= CK.Poco.Exc.Json.Export.PocoJsonExportOptions.Default;" ).NewLine()
                     .Append( writer =>
                     {
                         foreach( var f in type.Fields )
                         {
                             GenerateWritePropertyName( writer, f.Name );
                             GenerateWrite( writer, f.Type, f.PrivateFieldName );
                         }
                     } )
                     .Append( "w.WriteEndObject();" ).NewLine()
                     .CloseBlock();

            var toString = FunctionDefinition.Parse( "public override string ToString()" );
            if( pocoClass.FindFunction( toString.Key, false ) == null )
            {
                pocoClass
                    .CreateFunction( toString )
                    .GeneratedByComment().NewLine()
                    .Append( "var m = new System.Buffers.ArrayBufferWriter<byte>();" ).NewLine()
                    .Append( "using( var w = new System.Text.Json.Utf8JsonWriter( m ) )" ).NewLine()
                    .OpenBlock()
                    .Append( "WriteJson( w, CK.Poco.Exc.Json.Export.PocoJsonExportOptions.ToStringDefault );" ).NewLine()
                    .Append( "w.Flush();" ).NewLine()
                    .CloseBlock()
                    .Append( "return Encoding.UTF8.GetString( m.WrittenMemory.Span );" );
            }

        }

        void GenerateTypeHeader( ICodeWriter writer, IPocoType nonNullable )
        {
            var typeName = _nameMap.GetName( nonNullable );
            if( typeName.HasSimplifiedNames )
            {
                writer.Append( "w.WriteStringValue( options.UseSimplifiedTypes ? " )
                    .AppendSourceString( typeName.SimplifiedName )
                    .Append( " : " ).AppendSourceString( typeName.Name ).Append( " );" ).NewLine();
            }
            else
            {
                writer.Append( "w.WriteStringValue( " )
                    .AppendSourceString( typeName.Name ).Append( " );" ).NewLine();
            }
        }

        void GenerateWriteAny()
        {
            _pocoDirectory
                .GeneratedByComment()
                .Append( @"
internal static void WriteAnyJson( System.Text.Json.Utf8JsonWriter w, object o, CK.Poco.Exc.Json.Export.PocoJsonExportOptions options )
{
    w.WriteStartArray();
    var t = o.GetType();
    if( t.IsValueType )
    {
        if( t.IsEnum )
        {
            switch( o )
            {
                " ).CreatePart( out var enumCases ).Append( @"
                default: w.ThrowJsonException( $""Unregistered enumeration type: {t}"" ); break;
            }
        }
        else if( t.Name.StartsWith( ""ValueTuple`"", StringComparison.Ordinal ) && t.Namespace == ""System"" )
        {
            switch( o )
            {
                " ).CreatePart( out var valueTupleCases ).Append( @"
                default: w.ThrowJsonException( $""Unregistered ValueTuple: {t}"" ); break;
            }
        }
        else switch( o )
        {
            " ).CreatePart( out var basicValueTypeCases ).Append( @"
            " ).CreatePart( out var namedRecordCases ).Append( @"
            default: w.ThrowJsonException( $""Unregistered value type: {t}"" ); break;
        }
    }
    else
    {
        switch( o )
        {
            " ).CreatePart( out var basicRefTypeCases ).Append( @"
            case IPoco:
            {
                switch( o )
                {
                    " ).CreatePart( out var pocoCases ).Append( @"
                }
                break;
            }
            case Array:
            {
                " ).CreatePart( out var arrayMatch ).Append( @"
                break;
            }
            " ).CreatePart( out var collectionCases ).Append( @"
            default: w.ThrowJsonException( $""Unregistered type: {t}"" ); break;
        }
    }
    w.WriteEndArray();
}" );
            foreach( var t in _nameMap.ExchangeableNonNullableTypes )
            {
                if( t.Kind == PocoTypeKind.Any || t.Kind == PocoTypeKind.AbstractIPoco || t.Kind == PocoTypeKind.UnionType ) continue;

                switch( t.Kind )
                {
                    case PocoTypeKind.Basic:
                        {
                            var part = t.Type.IsValueType ? basicValueTypeCases : basicRefTypeCases;
                            part.Append( "case " ).Append( t.ImplTypeName ).Append( " v:" )
                                .OpenBlock()
                                .Append( writer =>
                                {
                                    GenerateTypeHeader( writer, t );
                                    GenerateWrite( writer, t, "v" );
                                } )
                                .NewLine()
                                .Append( "break;" )
                                .CloseBlock();
                            break;
                        }

                    case PocoTypeKind.Enum:
                        {
                            enumCases.Append( "case " ).Append( t.ImplTypeName ).Append( " v:" )
                                .OpenBlock()
                                .Append( writer =>
                                {
                                    GenerateTypeHeader( writer, t );
                                    GenerateWrite( writer, t, "v" );
                                } )
                                .NewLine()
                                .Append( "break;" )
                                .CloseBlock();
                            break;
                        }

                    case PocoTypeKind.IPoco:
                        {
                            pocoCases.Append( "case " ).Append( t.ImplTypeName ).Append( " v:" )
                                .OpenBlock()
                                .Append( writer =>
                                {
                                    GenerateTypeHeader( writer, t );
                                    GenerateWrite( writer, t, "v" );
                                } )
                                .NewLine()
                                .Append( "break;" )
                                .CloseBlock();
                            break;
                        }
                }
            }
        }
    }
}
