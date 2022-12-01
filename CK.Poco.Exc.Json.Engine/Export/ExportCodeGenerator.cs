using CK.CodeGen;
using CK.Core;
using System;
using System.Numerics;

namespace CK.Setup.PocoJson
{
    /// <summary>
    /// The code writer delegate is in charge of generating the write code into a <see cref="System.Text.Json.Utf8JsonWriter"/>
    /// from a variable named "w" and a PocoJsonExportOptions variable named "options".
    /// </summary>
    /// <param name="write">The code writer to uses.</param>
    /// <param name="variableName">The variable name to write.</param>
    delegate void CodeWriter( ICodeWriter write, string variableName );

    sealed class ExportCodeGenerator
    {
        readonly ITypeScope _pocoDirectory;
        readonly ExchangeableTypeNameMap _nameMap;
        readonly ICSCodeGenerationContext _generationContext;
        // Writers are for the non nullable types, whether they are oblivious types
        // or not: writers for the same "oblivious family" will share the same function.
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
            if( t.Type.IsValueType )
            {
                if( t.IsNullable )
                {
                    writer.Append( "if( !" ).Append( variableName ).Append( ".HasValue ) w.WriteNullValue();" ).NewLine()
                          .Append( "else" )
                          .OpenBlock();
                    var v = $"CommunityToolkit.HighPerformance.Extensions.NullableExtensions.DangerousGetValueOrDefaultReference(ref {variableName})";
                    _writers[t.Index >> 1].Invoke( writer, v );
                    writer.CloseBlock();
                }
                else
                {
                    _writers[t.Index >> 1].Invoke( writer, variableName );
                }
            }
            else
            {
                // Since we are working in oblivious mode, any reference type MAY be null.
                writer.Append( "if( " ).Append( variableName ).Append( " == null ) w.WriteNullValue();" ).NewLine()
                      .Append( "else" )
                      .OpenBlock();
                _writers[t.Index >> 1].Invoke( writer, variableName );
                writer.CloseBlock();
            }
        }

        public bool Run( IActivityMonitor monitor )
        {
            RegisterWriters();
            GenerateWriteMethods( monitor );
            GenerateWriteAny();
            return true;

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
                                                                : GetObliviousCodeWriter( type );
                                break;
                            }
                        case PocoTypeKind.List:
                        case PocoTypeKind.HashSet:
                        case PocoTypeKind.Dictionary:
                        case PocoTypeKind.Record:
                        case PocoTypeKind.AnonymousRecord:
                            _writers[type.Index >> 1] = GetObliviousCodeWriter( type );
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
                    writer.Append( "PocoDirectory_CK.WriteAnyJson( w, " ).Append( variableName ).Append( ", options );" );
                }

                static void PocoWriter( ICodeWriter writer, string variableName )
                {
                    writer.Append( "((PocoJsonExportSupport.IWriter)" ).Append( variableName ).Append( ").WriteJson( w, options );" );
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

                static CodeWriter GetObliviousCodeWriter( IPocoType type )
                {
                    if( type.ImplTypeName != type.ObliviousType.ImplTypeName )
                    {
                        // The type is an adapter that is a type.ObliviousType.ImplTypeName.
                        return ( writer, v ) => writer.Append( "PocoDirectory_CK.WriteJson_" )
                                                      .Append( type.ObliviousType.Index )
                                                      .Append( "( w, ref System.Runtime.CompilerServices.Unsafe.AsRef<" )
                                                      .Append( type.ObliviousType.ImplTypeName )
                                                      .Append( ">(" )
                                                      .Append( v )
                                                      .Append( "), options );" );
                    }
                    return ( writer, v ) => writer.Append( "PocoDirectory_CK.WriteJson_" )
                                                  .Append( type.ObliviousType.Index )
                                                  .Append( "( w, ref " )
                                                  .Append( v ).Append( ", options );" );
                }

            }

            // Used by GenerateWriteMethods.GeneratePocoWriteMethod for the WriteJson( w, withType, options )
            // and  GenerateWriteAny().
            void GenerateTypeHeader( ICodeWriter writer, IPocoType nonNullable, bool honorOption )
            {
                var typeName = _nameMap.GetName( nonNullable );
                if( honorOption ) writer.Append( $"if(!options.TypeLess)" );
                if( typeName.HasSimplifiedNames )
                {
                    writer.Append( "w.WriteStringValue(options.UseSimplifiedTypes?" )
                        .AppendSourceString( typeName.SimplifiedName )
                        .Append( ":" );
                }
                else
                {
                    writer.Append( "w.WriteStringValue(" );
                }
                writer.AppendSourceString( typeName.Name ).Append( ");" ).NewLine();
            }

            // Step 2: The actual Write methods are implemented only for the Exchangeable, NonNullable, and Oblivious types.
            void GenerateWriteMethods( IActivityMonitor monitor )
            {
                foreach( var type in _nameMap.ExchangeableNonNullableObliviousTypes )
                {
                    switch( type.Kind )
                    {
                        case PocoTypeKind.IPoco:
                            GeneratePocoWriteMethod( monitor, _generationContext, (IPrimaryPocoType)type );
                            break;
                        case PocoTypeKind.AnonymousRecord:
                            GenerateAnonymousRecordWriteMethod( _pocoDirectory, (IRecordPocoType)type );
                            break;
                        case PocoTypeKind.Record:
                            GenerateNamedRecordWriteMethod( _pocoDirectory, (IRecordPocoType)type );
                            break;
                        case PocoTypeKind.Array:
                            ICollectionPocoType tA = (ICollectionPocoType)type;
                            if( tA.ItemTypes[0].Type != typeof( byte ) )
                            {
                                GenerateListOrArrayWriteMethod( _pocoDirectory, tA );
                            }
                            break;
                        case PocoTypeKind.List:
                            GenerateListOrArrayWriteMethod( _pocoDirectory, (ICollectionPocoType)type );
                            break;
                        case PocoTypeKind.HashSet:
                            GenerateHashSetWriteMethod( _pocoDirectory, (ICollectionPocoType)type );
                            break;
                        case PocoTypeKind.Dictionary:
                            GenerateDictionaryWriteMethod( _pocoDirectory, (ICollectionPocoType)type );
                            break;
                    }
                }

                void GenerateWriteJsonMethodHeader( ITypeScope code, IPocoType type )
                {
                    code.Append( "internal static void WriteJson_" )
                        .Append( type.Index )
                        .Append( "( System.Text.Json.Utf8JsonWriter w, ref " )
                        .Append( type.ImplTypeName ).Append( " v, CK.Poco.Exc.Json.Export.PocoJsonExportOptions options )" );
                }

                void GenerateDictionaryWriteMethod( ITypeScope code, ICollectionPocoType type )
                {
                    GenerateWriteJsonMethodHeader( code, type );
                    code.OpenBlock();
                    if( type.ItemTypes[0].Type == typeof( string ) )
                    {
                        code.Append( "w.WriteStartObject();" ).NewLine()
                                        .Append( "foreach( var item in v )" )
                                        .OpenBlock()
                                        .Append( "w.WritePropertyName( item.Key );" ).NewLine()
                                        .Append( "var vLoc = item.Value;" ).NewLine()
                                        .Append( writer => GenerateWrite( writer, type.ItemTypes[1], "vLoc" ) ).NewLine()
                                        .CloseBlock()
                                        .Append( "w.WriteEndObject();" );
                    }
                    else
                    {
                        code.Append( "w.WriteStartArray();" ).NewLine()
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
                    code.CloseBlock();
                }

                void GenerateHashSetWriteMethod( ITypeScope code, ICollectionPocoType type )
                {
                    GenerateWriteJsonMethodHeader( code, type );
                    code.OpenBlock();
                    code.Append( "w.WriteStartArray();" ).NewLine()
                        .Append( "foreach( var item in v )" )
                        .OpenBlock()
                        .Append( "var loc = item;" ).NewLine()
                        .Append( writer => GenerateWrite( writer, type.ItemTypes[0], "loc" ) )
                        .CloseBlock()
                        .Append( "w.WriteEndArray();" ).NewLine()
                        .CloseBlock();
                }

                void GenerateListOrArrayWriteMethod( ITypeScope code, ICollectionPocoType type )
                {
                    GenerateWriteJsonMethodHeader( code, type );
                    code.OpenBlock();
                    if( type.Kind == PocoTypeKind.Array )
                    {
                        code.Append( "var a = v.AsSpan();" ).NewLine();
                    }
                    else
                    {
                        code.Append( "var a = System.Runtime.InteropServices.CollectionsMarshal.AsSpan( v );" ).NewLine();
                    }
                    code.Append( "w.WriteStartArray();" ).NewLine()
                        .Append( "for( int i = 0; i < a.Length; ++i )" )
                        .OpenBlock()
                        .Append( writer => GenerateWrite( writer, type.ItemTypes[0], "a[i]" ) )
                        .CloseBlock()
                        .Append( "w.WriteEndArray();" ).NewLine()
                        .CloseBlock();
                }

                void GenerateAnonymousRecordWriteMethod( ITypeScope code, IRecordPocoType type )
                {
                    GenerateWriteJsonMethodHeader( code, type );
                    code.OpenBlock()
                                  .Append( "w.WriteStartArray();" ).NewLine();
                    foreach( var f in type.Fields )
                    {
                        GenerateWrite( code, f.Type, $"v.{f.Name}" );
                    }
                    code.Append( "w.WriteEndArray();" ).NewLine()
                                  .CloseBlock();
                }

                void GenerateWritePropertyName( ICodeWriter writer, string name )
                {
                    writer.Append( "w.WritePropertyName( options.UseCamelCase ? " )
                          .AppendSourceString( System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName( name ) )
                          .Append( " : " )
                          .AppendSourceString( name )
                          .Append( " );" ).NewLine();
                }

                void GenerateNamedRecordWriteMethod( ITypeScope code, IRecordPocoType type )
                {
                    GenerateWriteJsonMethodHeader( code, type );
                    code.OpenBlock()
                                  .Append( "w.WriteStartObject();" ).NewLine();
                    foreach( var f in type.Fields )
                    {
                        GenerateWritePropertyName( code, f.Name );
                        GenerateWrite( code, f.Type, $"v.{f.Name}" );
                        code.NewLine();
                    }
                    code.Append( "w.WriteEndObject();" ).NewLine()
                                  .CloseBlock();
                }

                void GeneratePocoWriteMethod( IActivityMonitor monitor, ICSCodeGenerationContext generationContext, IPrimaryPocoType type )
                {
                    // Each Poco class is a PocoJsonExportSupport.IWriter.
                    var pocoClass = generationContext.Assembly.FindOrCreateAutoImplementedClass( monitor, type.FamilyInfo.PocoClass );
                    pocoClass.Definition.BaseTypes.Add( new ExtendedTypeName( "PocoJsonExportSupport.IWriter" ) );

                    // The Write method.
                    // The write part will be filled with the properties (name and writer code).
                    pocoClass.Append( "public void WriteJson( System.Text.Json.Utf8JsonWriter w, bool withType, CK.Poco.Exc.Json.Export.PocoJsonExportOptions options )" )
                             .OpenBlock()
                             .GeneratedByComment().NewLine()
                             .Append( "if( withType )" )
                             .OpenBlock()
                             .Append( "w.WriteStartArray();" ).NewLine()
                             .Append( writer => GenerateTypeHeader( writer, type, honorOption: false ) )
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
            }

            // Step 3: Generating the WriteAnyJson that routes any object to its registered Oblivious type.
            //         This is basically a big switch case on the object.GetType() except that it is broken
            //         into smaller pieces for better performance.
            void GenerateWriteAny()
            {
                _pocoDirectory
                    .GeneratedByComment()
                    .Append( @"
internal static void WriteAnyJson( System.Text.Json.Utf8JsonWriter w, object o, CK.Poco.Exc.Json.Export.PocoJsonExportOptions options )
{
    if( !options.TypeLess ) w.WriteStartArray();
    var t = o.GetType();
    if( t.IsValueType )
    {
        if( t.IsEnum )
        {
            switch( o )
            {
                " ).CreatePart( out var enumCases ).Append( @"
                default: w.ThrowJsonException( $""Unregistered enumeration type: {t.ToCSharpName(false)}"" ); break;
            }
        }
        else if( t.Name.StartsWith( ""ValueTuple`"", StringComparison.Ordinal ) && t.Namespace == ""System"" )
        {
            switch( o )
            {
                " ).CreatePart( out var valueTupleCases ).Append( @"
                default: w.ThrowJsonException( $""Unregistered ValueTuple: {t.ToCSharpName(false)}"" ); break;
            }
        }
        else switch( o )
        {
            " ).CreatePart( out var basicValueTypeCases ).Append( @"
            " ).CreatePart( out var namedRecordCases ).Append( @"
            default: w.ThrowJsonException( $""Unregistered value type: {t.ToCSharpName(false)}"" ); break;
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
                switch( o )
                {
                    " ).CreatePart( out var arrayCases ).Append( @"
                    default: w.ThrowJsonException( $""Unregistered array type: {t.ToCSharpName(false)}"" ); break;
                }
                break;
            }
            " ).CreatePart( out var collectionCases ).Append( @"
            default: w.ThrowJsonException( $""Unregistered type: {t.ToCSharpName(false)}"" ); break;
        }
    }
    if( !options.TypeLess ) w.WriteEndArray();
}" );
                // Builds the different sorters for cases that must be ordered: arrays and collections
                // only since these are the only reference types except the basic ones (that moreover
                // is currently the single 'string').
                var arrays = new NominalReferenceTypeSorter();
                var collections = new NominalReferenceTypeSorter();

                foreach( var t in _nameMap.ExchangeableNonNullableObliviousTypes )
                {
                    if( t.Kind == PocoTypeKind.Any
                        || t.Kind == PocoTypeKind.AbstractIPoco
                        || t.Kind == PocoTypeKind.UnionType
                        || t.ObliviousType != t )
                    {
                        continue;
                    }
                    switch( t.Kind )
                    {
                        case PocoTypeKind.Basic:
                            {
                                var part = t.Type.IsValueType ? basicValueTypeCases : basicRefTypeCases;
                                WriteCase( part, t );
                                break;
                            }

                        case PocoTypeKind.Enum:
                            {
                                WriteCase( enumCases, t );
                                break;
                            }

                        case PocoTypeKind.Array:
                            {
                                arrays.Add( t );
                                break;
                            }

                        case PocoTypeKind.IPoco:
                            {
                                WriteCase( pocoCases, t );
                                break;
                            }
                        case PocoTypeKind.List:
                        case PocoTypeKind.HashSet:
                        case PocoTypeKind.Dictionary:
                            collections.Add( t );
                            break;
                        case PocoTypeKind.AnonymousRecord:
                            {
                                // Switch case doesn't work with (tuple, syntax).
                                WriteCase( valueTupleCases, t, t.Type.ToCSharpName( useValueTupleParentheses: false ) );
                                break;
                            }
                        case PocoTypeKind.Record:
                            {
                                WriteCase( namedRecordCases, t );
                                break;
                            }
                        default:
                            Throw.NotSupportedException( t.ToString() );
                            break;
                    }
                }

                foreach( var t in arrays.SortedTypes ) WriteCase( arrayCases, t );
                foreach( var t in collections.SortedTypes ) WriteCase( collectionCases, t );

                return;

                void WriteCase( ITypeScopePart code, IPocoType t, string? typeName = null )
                {
                    code.Append( "case " ).Append( typeName ?? t.ImplTypeName ).Append( " v:" )
                        .OpenBlock()
                        .Append( writer =>
                        {
                            GenerateTypeHeader( writer, t, true );
                            GenerateWrite( writer, t, "v" );
                        } )
                        .NewLine()
                        .Append( "break;" )
                        .CloseBlock();
                }
            }
        }
    }
}
