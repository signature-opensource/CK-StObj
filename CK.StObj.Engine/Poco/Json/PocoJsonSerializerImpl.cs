using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

#nullable enable

namespace CK.Setup.Json
{
    /// <summary>
    /// Implements the Json serialization. This class extends the Poco classes to support
    /// the API exposed as extension methods by the CK.Core.PocoJsonSerializer static
    /// class (in CK.Poco.Json).
    /// <para>
    /// There is no CK.Poco.Json.Engine: the Json serializer is here but triggered by the
    /// existence of the CK.Poco.Json package.
    /// </para>
    /// <para>
    /// This instantiates the Runtime <see cref="Json.JsonSerializationCodeGen"/> service and exposes it.
    /// </para>
    /// </summary>
    public partial class PocoJsonSerializerImpl : ICSCodeGenerator
    {
        // Filled when the Poco properties types have been registered on the JsonSerializationCodeGen.
        // These actions generate the actual red/write code of the Read and Write methods once the
        // Json types have been finalized.
        readonly List<Action> _finalReadWrite = new List<Action>();

        /// <summary>
        /// Instantiates the <see cref="JsonSerializationCodeGen"/> and exposes it in the services.
        /// Extends PocoDirectory_CK, the factories and the Poco classes.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="c">The code generation context.</param>
        /// <returns>Always <see cref="CSCodeGenerationResult.Success"/>.</returns>
        public CSCodeGenerationResult Implement( IActivityMonitor monitor, ICSCodeGenerationContext c )
        {
            var pocoDirectory = c.Assembly.Code.Global.FindOrCreateAutoImplementedClass( monitor, typeof( PocoDirectory ) );
            var jsonCodeGen = new JsonSerializationCodeGen( monitor, pocoDirectory );
            // Exposes this as a service to others.
            monitor.Info( "Exposing JsonSerializationCodeGen in CurrentRun services." );
            c.CurrentRun.ServiceContainer.Add( jsonCodeGen );
            return new CSCodeGenerationResult( nameof( AllowAllPocoTypes ) );
        }

        // Step 1: Allows the registered Poco types on the JsonSerializationCodeGen.
        //         Creates the CodeReader/Writer for the Poco classes that use the Poco.Write
        //         and the Poco deserialization constructor that will be generated in the next step.
        CSCodeGenerationResult AllowAllPocoTypes( IActivityMonitor monitor, ICSCodeGenerationContext c, IPocoDirectory pocoSupport, JsonSerializationCodeGen jsonCodeGen )
        { 
            if( pocoSupport.Families.Count == 0 )
            {
                monitor.Info( "No Poco available. Skipping Poco serialization code generation." );
                return new CSCodeGenerationResult( nameof( FinalizeJsonSupport ) );
            }

            using( monitor.OpenInfo( $"Allowing Poco Json serialization C# code generation for {pocoSupport.Families.Count} Pocos." ) )
            {
                // IPoco and IClosedPoco are not in the "OtherInterfaces".
                jsonCodeGen.AllowInterfaceToUntyped( typeof( IPoco ) );
                jsonCodeGen.AllowInterfaceToUntyped( typeof( IClosedPoco ) );
                // Maps the "other Poco interfaces" to "Untyped" object.
                foreach( var other in pocoSupport.OtherInterfaces )
                {
                    jsonCodeGen.AllowInterfaceToUntyped( other.Key );
                }

                bool success = true;
                // Registers TypeInfo for the PocoClass and maps its interfaces to the PocoClass.
                foreach( var root in pocoSupport.Families )
                {
                    var typeInfo = jsonCodeGen.AllowTypeInfo( root.PocoClass, root.Name, root.PreviousNames )?.Configure(
                                    ( ICodeWriter write, string variableName )
                                            => write.Append( "((PocoJsonSerializer.IWriter)" ).Append( variableName ).Append( ").Write( w, false, options );" ),
                                    ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                                    {
                                        if( !assignOnly )
                                        {
                                            if( isNullable )
                                            {
                                                read.Append( "if( " ).Append( variableName ).Append( " != null ) " ).NewLine();
                                            }
                                            read.Append( "((" ).AppendCSharpName( root.PocoClass, true, true, true ).Append( ')' ).Append( variableName ).Append( ')' ).Append( ".Read( ref r, options );" );
                                            if( isNullable )
                                            {
                                                read.NewLine().Append( "else" ).NewLine();
                                                assignOnly = true;
                                            }
                                        }
                                        if( assignOnly )
                                        {
                                            read.Append( variableName )
                                                .Append( " = new " ).AppendCSharpName( root.PocoClass, true, true, true )
                                                .Append( "( ref r, options );" );
                                        }
                                    } );
                    if( typeInfo != null )
                    {
                        foreach( var i in root.Interfaces )
                        {
                            jsonCodeGen.AllowTypeAlias( i.PocoInterface.GetNullableTypeTree(), typeInfo );
                        }
                    }
                    else
                    {
                        success = false;
                    }
                }
                return success
                        ? new CSCodeGenerationResult( nameof( GeneratePocoSupport ) )
                        : CSCodeGenerationResult.Failed;
            }
        }

        // Step 2: Generates the Read & Write methods.
        //         This is where the Poco's properties types are transitively allowed.
        CSCodeGenerationResult GeneratePocoSupport( IActivityMonitor monitor, ICSCodeGenerationContext c, IPocoDirectory pocoSupport, JsonSerializationCodeGen jsonCodeGen )
        {
            using var gLog = monitor.OpenInfo( $"Generating C# Json serialization code." );

            {
                var ns = c.Assembly.Code.Global.FindOrCreateNamespace( "CK.Core" );
                ns.GeneratedByComment();
                ns.Append( @"
        static class JsonGeneratedHelperExtension
        {
            public static void ThrowJsonException( this ref System.Text.Json.Utf8JsonReader r, string m )
            {
                ThrowJsonException( m );
            }

            public static void ThrowJsonException( string m )
            {
                throw new System.Text.Json.JsonException( m );
            }
        }" ).NewLine();
            }


            bool success = true;
            // Generates the factory and the Poco class code.
            foreach( var root in pocoSupport.Families )
            {
                var factory = c.Assembly.Code.Global.FindOrCreateAutoImplementedClass( monitor, root.PocoFactoryClass );
                foreach( var i in root.Interfaces )
                {
                    var interfaceName = i.PocoInterface.ToCSharpName();
                    var readerName = "PocoJsonSerializer.IFactoryReader<" + interfaceName + ">";

                    factory.Definition.BaseTypes.Add( new ExtendedTypeName( readerName ) );
                    factory.Append( interfaceName ).Append( ' ' ).Append( readerName ).Append( ".Read( ref System.Text.Json.Utf8JsonReader r, PocoJsonSerializerOptions options )" ).NewLine()
                            .Append( " => r.TokenType == System.Text.Json.JsonTokenType.Null ? null : new " )
                            .Append( root.PocoClass.Name ).Append( "( ref r, options );" ).NewLine();

                }
                factory.Append( "public IPoco ReadTyped( ref System.Text.Json.Utf8JsonReader r, PocoJsonSerializerOptions options ) => new " ).Append( root.PocoClass.Name ).Append( "( ref r, options );" ).NewLine();

                var pocoClass = c.Assembly.Code.Global.FindOrCreateAutoImplementedClass( monitor, root.PocoClass );

                // Generates the Poco class Read and Write methods.
                // UnionTypes on properties are registered.
                success &= ExtendPocoClass( monitor, root, jsonCodeGen, pocoClass );
            }
            return success
                    ? new CSCodeGenerationResult( nameof( FinalizeJsonSupport ) )
                    : CSCodeGenerationResult.Failed;
        }

        bool ExtendPocoClass( IActivityMonitor monitor, IPocoFamilyInfo pocoInfo, JsonSerializationCodeGen jsonCodeGen, ITypeScope pocoClass )
        {
            bool success = true;

            // Each Poco class is a IWriter and has a constructor that accepts a Utf8JsonReader.
            pocoClass.Definition.BaseTypes.Add( new ExtendedTypeName( "CK.Core.PocoJsonSerializer.IWriter" ) );

            // Defines ToString() to return the Json representation only if it is not already defined.
            var toString = FunctionDefinition.Parse( "public override string ToString()" );
            if( pocoClass.FindFunction( toString.Key, false ) == null )
            {
                pocoClass
                    .CreateFunction( toString )
                    .GeneratedByComment().NewLine()
                    .Append( "var m = new System.Buffers.ArrayBufferWriter<byte>();" ).NewLine()
                    .Append( "using( var w = new System.Text.Json.Utf8JsonWriter( m ) )" ).NewLine()
                    .OpenBlock()
                    .Append( "Write( w, false, null );" ).NewLine()
                    .Append( "w.Flush();" ).NewLine()
                    .CloseBlock()
                    .Append( "return Encoding.UTF8.GetString( m.WrittenMemory.Span );" );
            }

            // The Write method:
            //  - The writeHeader part may contain the ECMAScriptStandard non compliant exception (if it appears that a UnionType is not compliant).
            //  - The write part will be filled with the properties (name and writer code).
            pocoClass.Append( "public void Write( System.Text.Json.Utf8JsonWriter w, bool withType, PocoJsonSerializerOptions options )" )
                     .OpenBlock()
                     .GeneratedByComment().NewLine()
                     .CreatePart( out var writeHeader )
                     .Append( "bool usePascalCase = options != null && options.ForJsonSerializer.PropertyNamingPolicy != System.Text.Json.JsonNamingPolicy.CamelCase;" ).NewLine()
                     .Append( "if( withType ) { w.WriteStartArray(); w.WriteStringValue( " ).AppendSourceString( pocoInfo.Name ).Append( "); }" ).NewLine()
                     .Append( "w.WriteStartObject();" )
                     .CreatePart( out var write )
                     .Append( "w.WriteEndObject();" ).NewLine()
                     .Append( "if( withType ) w.WriteEndArray();" ).NewLine()
                     .CloseBlock();

            // The constructor calls the private Read method.
            pocoClass.Append( "public " ).Append( pocoClass.Name ).Append( "( ref System.Text.Json.Utf8JsonReader r, PocoJsonSerializerOptions options ) : this()" )
                 .OpenBlock()
                 .Append( "Read( ref r, options );" )
                 .CloseBlock();

            // Poco has a Read method but it is not (currently) exposed.
            // This returns two parts: a header (to inject the ECMAScriptStandard non compliant
            // exception if it appears that a UnionType is not compliant) and the switch-case part on the
            // property names with their reader code.
            var (readHeader, read) = GenerateReadBody( pocoInfo, pocoClass );

            bool isECMAScriptStandardCompliant = true;
            var jsonProperties = new PocoJsonPropertyInfo[pocoInfo.PropertyList.Count];
            foreach( var p in pocoInfo.PropertyList )
            {
                var mainHandler = jsonCodeGen.GetHandler( p.PropertyNullableTypeTree );
                if( mainHandler == null )
                {
                    success = false;
                    continue;
                }

                PocoJsonPropertyInfo? pJ;
                if( p.PropertyUnionTypes.Any() )
                {
                    pJ = HandleUnionType( p, monitor, jsonCodeGen, write, read, ref isECMAScriptStandardCompliant, mainHandler );
                    if( pJ == null )
                    {
                        success = false;
                        break;
                    }
                }
                else
                {
                    var handlers = new[] { mainHandler };
                    pJ = new PocoJsonPropertyInfo( p, handlers, mainHandler.HasECMAScriptStandardJsonName && isECMAScriptStandardCompliant ? handlers : null );
                    // Actual Read/Write generation cannot be done here (it must be postponed).
                    // This loop registers/allows the poco property types (the call to GetHandler triggers
                    // the type registration) but writing them requires to know whether those types are final or not .
                    // We store (using closure) the property, the write and read parts and the handler(s)
                    // (to avoid another lookup) and wait for the FinalizeJsonSupport to be called.
                    _finalReadWrite.Add( () =>
                    {
                        var fieldName = "_v" + p.Index;

                        write.Append( "w.WritePropertyName( usePascalCase ? " )
                             .AppendSourceString( p.Name )
                             .Append( " : " )
                             .AppendSourceString( System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName( p.Name ) )
                             .Append( " );" ).NewLine();
                        mainHandler.GenerateWrite( write, fieldName );
                        write.NewLine();

                        var camel = System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName( p.Name );
                        if( camel != p.Name )
                        {
                            read.Append( "case " ).AppendSourceString( camel ).Append( ": " );
                        }
                        read.Append( "case " ).AppendSourceString( p.Name ).Append( ": " )
                            .OpenBlock();
                        mainHandler.GenerateRead( read, fieldName, assignOnly: !p.IsReadOnly );
                        read.Append( "break; " )
                            .CloseBlock();
                    } );
                }
                p.AddAnnotation( pJ );
                jsonProperties[p.Index] = pJ;
            }
            if( success )
            {
                if( !isECMAScriptStandardCompliant )
                {
                    writeHeader.And( readHeader ).Append( "if( options != null && options.Mode == PocoJsonSerializerMode.ECMAScriptStandard ) Throw.NotSupportedException( \"Poco '" )
                                                    .Append( pocoInfo.Name )
                                                    .Append( "' is not compliant with the ECMAScripStandard mode.\" );" ).NewLine();
                }
                var info = new PocoJsonInfo( pocoInfo, isECMAScriptStandardCompliant, jsonProperties );
                pocoInfo.AddAnnotation( info );
            }
            return success;
        }

        /// <summary>
        /// Generates the "public void Read( ref System.Text.Json.Utf8JsonReader r, PocoJsonSerializerOptions options )" method
        /// that handles a potential array definition with a check of the type and the loop
        /// over the properties: the returned part must be filled with the case statements on
        /// the property names.
        /// </summary>
        /// <param name="pocoInfo">The poco root information.</param>
        /// <param name="pocoClass">The target class to generate.</param>
        /// <returns>A header part and the part in the switch statement.</returns>
        static (ITypeScopePart,ITypeScopePart) GenerateReadBody( IPocoFamilyInfo pocoInfo, ITypeScope pocoClass )
        {
            pocoClass.GeneratedByComment().NewLine().Append( "public void Read( ref System.Text.Json.Utf8JsonReader r, PocoJsonSerializerOptions options )" )
              .OpenBlock()
              .GeneratedByComment()
              .CreatePart( out var readHeader )
              .Append( @"
bool isDef = r.TokenType == System.Text.Json.JsonTokenType.StartArray;
if( isDef )
{
    r.Read();
    string name = r.GetString();
    if( name != " ).AppendSourceString( pocoInfo.Name );
            if( pocoInfo.PreviousNames.Count > 0 )
            {
                pocoClass.Append( " && !" ).AppendArray( pocoInfo.PreviousNames ).Append( ".Contains( name )" );
            }
            pocoClass.Append( @" )
    {
        throw new System.Text.Json.JsonException( ""Expected '""+ " ).AppendSourceString( pocoInfo.Name ).Append( @" + $""' Poco type, but found '{name}'."" );
    }
    r.Read();
}
if( r.TokenType != System.Text.Json.JsonTokenType.StartObject ) r.ThrowJsonException( ""Expecting '{' to start a Poco."" );
r.Read();
while( r.TokenType == System.Text.Json.JsonTokenType.PropertyName )
{
    var n = r.GetString();
    r.Read();
    switch( n )
    {
" ).NewLine();
            var read = pocoClass.CreatePart();
            pocoClass.Append( @"
        default:
        {
            var t = r.TokenType; 
            if( t == System.Text.Json.JsonTokenType.StartObject || t == System.Text.Json.JsonTokenType.StartArray )
            {
                r.Skip();
            }
            r.Read();
            break;
        }
    }
}
if( r.TokenType != System.Text.Json.JsonTokenType.EndObject ) r.ThrowJsonException( ""Expecting '}' to end a Poco."" );
r.Read();
if( isDef )
{
    if( r.TokenType != System.Text.Json.JsonTokenType.EndArray ) r.ThrowJsonException( ""Expecting ']' to end a Poco array."" );
    r.Read();
}
" ).CloseBlock();
            return (readHeader,read);
        }

        // Step 3: Calls JsonSerializationCodeGen.FinalizeCodeGeneration that will generate
        //         the global Read & Write of "untyped" objects method. 
        void FinalizeJsonSupport( IActivityMonitor monitor, JsonSerializationCodeGen jsonCodeGen )
        {
            using var g = monitor.OpenInfo( $"Finalizing Json serialization C# code." );
            if( jsonCodeGen.FinalizeCodeGeneration( monitor ) )
            {
                foreach( var a in _finalReadWrite ) a();
            }
        }



    }
}
