using CK.CodeGen;
using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization.Metadata;

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
        readonly IPocoTypeSystem _typeSystem;
        readonly ICSCodeGenerationContext _generationContext;
        readonly CodeWriter[] _writers;

        public ExportCodeGenerator( ITypeScope pocoDirectory, IPocoTypeSystem typeSystem, ICSCodeGenerationContext generationContext )
        {
            _pocoDirectory = pocoDirectory;
            _typeSystem = typeSystem;
            _generationContext = generationContext;
            _writers = new CodeWriter[typeSystem.AllNonNullableTypes.Count];
        }

        void GenerateNonNullableWrite( ICodeWriter w, IPocoType nonNullable, string variableName )
        {
            Debug.Assert( !nonNullable.IsNullable && (nonNullable.Index & 1) == 0 );
            if( !nonNullable.Type.IsValueType )
            {
                w.Append( "if( " ).Append( variableName )
                    .Append( " == null ) w.ThrowJsonException( \"Unexpected null for non nullable '" )
                    .Append( nonNullable.ToString() ).Append( "'.\" );" )
                    .NewLine();
            }
            _writers[nonNullable.Index >> 1].Invoke( w, variableName );
        }

        void GenerateNullableWrite( ICodeWriter w, IPocoType nullable, string variableName )
        {
            Debug.Assert( nullable.IsNullable && (nullable.Index & 1) == 1 );
            w.Append( "if( " ).Append( variableName ).Append( " == null ) w.WriteNullValue();" ).NewLine();
            _writers[nullable.Index >> 1].Invoke( w, variableName );
        }

        void GenerateWrite( ICodeWriter w, IPocoType t, string variableName )
        {
            if( t.IsNullable ) GenerateNullableWrite( w, t, variableName );
            else GenerateNonNullableWrite( w, t, variableName );
}

        public bool Run( IActivityMonitor monitor )
        {
            using( monitor.OpenInfo( $"Generating {_typeSystem.AllNonNullableTypes.Count} Json code writers." ) )
            {
                int idx = 0;
                foreach( var type in _typeSystem.AllNonNullableTypes )
                {
                    switch( type.Kind )
                    {
                        case PocoTypeKind.Any:
                            _writers[idx] = ( writer, v ) => writer.Append( "PocoDirectory_CK.WriteAnyJson( w, " ).Append( v ).Append( ", options );" );
                            break;
                        case PocoTypeKind.IPoco:
                            _writers[idx] = ( writer, v ) => writer.Append( "((PocoJsonExportSupport.IWriter)" ).Append( v ).Append( ").Write( w, options );" );
                            GeneratePocoWriteMethod( monitor, (IPrimaryPocoType)type );
                            break;
                        case PocoTypeKind.Enum:
                            var tE = (IEnumPocoType)type;
                            _writers[idx] = ( writer, v ) => writer.Append( "w.WriteNumberValue( (" )
                                                                   .Append( tE.UnderlyingType.CSharpName )
                                                                   .Append( ')' ).Append( v ).Append( " );" );
                            break;

                    }
                    ++idx;
                }
            }
            return true;
        }

        void GeneratePocoWriteMethod( IActivityMonitor monitor, IPrimaryPocoType type )
        {
            Debug.Assert( type.Type == type.FamilyInfo.PocoClass );
            // Each Poco class is a IWriter.
            var pocoClass = _generationContext.Assembly.FindOrCreateAutoImplementedClass( monitor, type.Type );
            pocoClass.Definition.BaseTypes.Add( new ExtendedTypeName( "PocoJsonExportSupport.IWriter" ) );

            // The Write method.
            // The write part will be filled with the properties (name and writer code).
            pocoClass.Append( "public void Write( System.Text.Json.Utf8JsonWriter w, bool withType, CK.Poco.Exc.Json.ExportPocoJsonExportOptions options )" )
                     .OpenBlock()
                     .GeneratedByComment().NewLine()
                     .Append( "if( withType ) { w.WriteStartArray(); w.WriteStringValue( " ).AppendSourceString( type.FamilyInfo.Name ).Append( "); }" ).NewLine()
                     .Append( "w.WriteStartObject();" )
                     .CreatePart( out var write )
                     .Append( "w.WriteEndObject();" ).NewLine()
                     .Append( "if( withType ) w.WriteEndArray();" ).NewLine()
                     .CloseBlock();
            foreach( var f in type.Fields )
            {
                write.Append( "w.WritePropertyName( options.UseCamelCase ? " )
                     .AppendSourceString( System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName( f.Name ) )
                     .Append( " : " )
                     .AppendSourceString( f.Name )
                     .Append( " );" ).NewLine();
                GenerateWrite( write, f.Type, f.PrivateFieldName );
            }
        }

        void GenerateDynamicWrite()
        {
            _pocoDirectory
                .GeneratedByComment()
                .Append( @"
internal static void WriteAnyJson( System.Text.Json.Utf8JsonWriter w, object o, CK.Poco.Exc.Json.Export.PocoJsonSerializerOptions options, bool throwOnNull = false )
{
    switch( o )
    {
        case null:
            if( throwOnNull ) PocoJsonExportSupport.ThrowJsonException( ""A null value appear where it should not. Writing JSON is impossible."" );
            w.WriteNullValue();
            break;" ).NewLine()
        .CreatePart( out var mappings ).Append( @"
        default: PocoJsonExportSupport.ThrowJsonException( $""Unregistered type '{o.GetType().AssemblyQualifiedName}'."" );
    }
}" );
        }

    }
}
