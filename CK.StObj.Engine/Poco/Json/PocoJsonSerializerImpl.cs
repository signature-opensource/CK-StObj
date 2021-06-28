using CK.CodeGen;
using CK.Core;
using CK.Text;
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
            var pocoDirectory = c.Assembly.FindOrCreateAutoImplementedClass( monitor, typeof( PocoDirectory ) );
            var jsonCodeGen = new JsonSerializationCodeGen( monitor, pocoDirectory );
            // Exposes this as a service to others.
            c.CurrentRun.ServiceContainer.Add( jsonCodeGen );
            return new CSCodeGenerationResult( nameof( AllowAllPocoTypes ) );
        }

        // Step 1: Allows the registered Poco types on the JsonSerializationCodeGen.
        //         Creates the CodeReader/Writer for the Poco classes that use the Poco.Write
        //         and the Poco deserialization constructor that will be generated in the next step.
        CSCodeGenerationResult AllowAllPocoTypes( IActivityMonitor monitor, ICSCodeGenerationContext c, IPocoSupportResult pocoSupport, JsonSerializationCodeGen jsonCodeGen )
        { 
            if( pocoSupport.Roots.Count == 0 )
            {
                monitor.Info( "No Poco available. Skipping Poco serialization code generation." );
                return new CSCodeGenerationResult( nameof( FinalizeJsonSupport ) );
            }

            // IPoco and IClosedPoco are not in the "OtherInterfaces".
            jsonCodeGen.AllowInterfaceToUntyped( typeof( IPoco ) );
            jsonCodeGen.AllowInterfaceToUntyped( typeof( IClosedPoco ) );

            // Registers TypeInfo for the PocoClass and maps its interfaces to the PocoClass.
            foreach( var root in pocoSupport.Roots )
            {
                var typeInfo = jsonCodeGen.AllowTypeInfo( root.PocoClass, root.Name, StartTokenType.Object, root.PreviousNames ).Configure(
                                ( ICodeWriter write, string variableName )
                                        => write.Append( variableName ).Append( ".Write( w, false, options );" ),
                                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                                {
                                    if( !assignOnly )
                                    {
                                        if( isNullable )
                                        {
                                            read.Append( "if( " ).Append( variableName ).Append( " != null ) " ).NewLine();
                                        }
                                        read.Append( "((" ).AppendCSharpName( root.PocoClass ).Append( ')' ).Append( variableName ).Append( ')' ).Append( ".Read( ref r, options );" );
                                        if( isNullable )
                                        {
                                            read.NewLine().Append( "else" ).NewLine();
                                            assignOnly = true;
                                        }
                                    }
                                    if( assignOnly )
                                    {
                                        read.Append( variableName )
                                            .Append( " = new " ).AppendCSharpName( root.PocoClass )
                                            .Append( "( ref r, options );" );
                                    }
                                } );
                foreach( var i in root.Interfaces )
                {
                    jsonCodeGen.AllowTypeAlias( i.PocoInterface, typeInfo );
                }
            }
            // Maps the "other Poco interfaces" to "Untyped" object.
            foreach( var other in pocoSupport.OtherInterfaces )
            {
                jsonCodeGen.AllowInterfaceToUntyped( other.Key );
            }

            return new CSCodeGenerationResult( nameof( GeneratePocoSupport ) );
        }

        // Step 2: Generates the Read & Write methods.
        //         This is where the Poco's properties types are transitively allowed.
        CSCodeGenerationResult GeneratePocoSupport( IActivityMonitor monitor, ICSCodeGenerationContext c, IPocoSupportResult pocoSupport, JsonSerializationCodeGen jsonCodeGen )
        {
            bool success = true;
            // Generates the factory and the Poco class code.
            foreach( var root in pocoSupport.Roots )
            {
                var factory = c.Assembly.FindOrCreateAutoImplementedClass( monitor, root.PocoFactoryClass );
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

                var pocoClass = c.Assembly.FindOrCreateAutoImplementedClass( monitor, root.PocoClass );

                // Generates the Poco class Read and Write methods.
                // UnionTypes on properties are registered.
                success &= ExtendPocoClass( monitor, root, jsonCodeGen, pocoClass );
            }
            return success
                    ? new CSCodeGenerationResult( nameof( FinalizeJsonSupport ) )
                    : CSCodeGenerationResult.Failed;
        }

        bool ExtendPocoClass( IActivityMonitor monitor, IPocoRootInfo pocoInfo, JsonSerializationCodeGen jsonCodeGen, ITypeScope pocoClass )
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
                var mainHandler = jsonCodeGen.GetHandler( p.PropertyType, p.IsNullable );
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

                        write.Append( "w.WritePropertyName( " ).AppendSourceString( p.PropertyName ).Append( " );" ).NewLine();
                        mainHandler.GenerateWrite( write, fieldName );
                        write.NewLine();

                        read.Append( "case " ).AppendSourceString( p.PropertyName ).Append( ": " )
                            .OpenBlock();
                        mainHandler.GenerateRead( read, fieldName, false );
                        read.Append( "break; " )
                            .CloseBlock();
                    } );
                }
                jsonProperties[p.Index] = pJ;
            }
            if( success )
            {
                if( !isECMAScriptStandardCompliant )
                {
                    writeHeader.And( readHeader ).Append( "if( options != null && options.Mode == PocoJsonSerializerMode.ECMAScriptStandard ) throw new NotSupportedException( \"Poco '" )
                                                    .Append( pocoInfo.Name )
                                                    .Append( "' is not compliant with the ECMAScripStandard mode.\" );" ).NewLine();
                }
                var info = new PocoJsonInfo( pocoInfo, isECMAScriptStandardCompliant, jsonProperties );
                pocoInfo.AddAnnotation( pocoInfo );
            }
            return success;
        }

        PocoJsonPropertyInfo? HandleUnionType( IPocoPropertyInfo p,
                                               IActivityMonitor monitor,
                                               JsonSerializationCodeGen jsonCodeGen,
                                               ITypeScopePart write,
                                               ITypeScopePart read,
                                               ref bool isPocoECMAScriptStandardCompliant,
                                               IJsonCodeGenHandler mainHandler )
        {
            // Analyses the UnionTypes and creates the handler for each of them.
            // - Forbids ambiguous mapping for ECMAScriptStandard: all numerics are mapped to "Number" or "BigInt" (and arrays or lists are arrays).
            // - The ECMAScriptStandard projected name must be unique (and is associated to its actual handler).
            var handlers = new List<IJsonCodeGenHandler>();
            var checkDuplicatedStandardName = new Dictionary<string, List<IJsonCodeGenHandler>>();
            // Gets all the handlers and build groups of ECMAStandardJsnoName handlers (only if the Poco is still standard compliant).
            foreach( var union in p.PropertyUnionTypes )
            {
                var h = jsonCodeGen.GetHandler( union.Type, union.Kind.IsNullable() );
                if( h == null ) return null;
                handlers.Add( h );
                if( isPocoECMAScriptStandardCompliant && h.HasECMAScriptStandardJsonName )
                {
                    var n = h.ToNonNullHandler().ECMAScriptStandardJsonName;
                    if( checkDuplicatedStandardName.TryGetValue( n.Name, out var exists ) )
                    {
                        exists.Add( h );
                    }
                    else
                    {
                        checkDuplicatedStandardName.Add( n.Name, new List<IJsonCodeGenHandler>() { h } );
                    }
                }
            }
            // Analyze the groups (only if the Poco is still standard compliant).
            List<IJsonCodeGenHandler>? ecmaStandardReadhandlers = null;
            bool isECMAScriptStandardCompliant = isPocoECMAScriptStandardCompliant;
            if( isECMAScriptStandardCompliant )
            {
                foreach( var group in checkDuplicatedStandardName.Values )
                {
                    if( group.Count > 1 )
                    {
                        int idxCanocical = group.IndexOf( h => h.ECMAScriptStandardJsonName.IsCanonical );
                        if( idxCanocical == -1 )
                        {
                            monitor.Warn( $"{p} UnionType '{group.Select( h => h.Type.ToCSharpName() ).Concatenate( "' ,'" )}' types mapped to the same ECMAScript standard name: '{group[0].ECMAScriptStandardJsonName.Name}' and none of them is the 'Canonical' form. De/serializing this Poco in 'ECMAScriptstandard' will throw a NotSupportedException." );
                            isECMAScriptStandardCompliant = false;
                            break;
                        }
                        var winner = group[idxCanocical];
                        monitor.Trace( $"{p} UnionType '{group.Select( h => h.Type.ToCSharpName() ).Concatenate( "' ,'" )}' types will be read as {winner.Type.ToCSharpName()} in ECMAScript standard name." );
                        if( ecmaStandardReadhandlers == null ) ecmaStandardReadhandlers = new List<IJsonCodeGenHandler>();
                        ecmaStandardReadhandlers.Add( winner );
                    }
                    else
                    {
                        monitor.Debug( $"{p} UnionType unambiguous mapping in ECMAScript standard name from '{group[0].ECMAScriptStandardJsonName.Name}' to '{group[0].Type.ToCSharpName()}'." );
                        if( ecmaStandardReadhandlers == null ) ecmaStandardReadhandlers = new List<IJsonCodeGenHandler>();
                        ecmaStandardReadhandlers.Add( group[0] );
                    }
                }
                isPocoECMAScriptStandardCompliant &= isECMAScriptStandardCompliant;
            }
            var info = new PocoJsonPropertyInfo( p, handlers, isECMAScriptStandardCompliant ? ecmaStandardReadhandlers : null );
            _finalReadWrite.Add( () =>
            {
                var fieldName = "_v" + info.PropertyInfo.Index;
                write.Append( "w.WritePropertyName( " ).AppendSourceString( info.PropertyInfo.PropertyName ).Append( " );" ).NewLine();
                if( info.IsJsonUnionType )
                {
                    // For write, instead of generating a switch pattern on the actual object's type with a lot of duplicated write
                    // blocks (all the numerics that will eventually call w.WriteNumber for instance), we use the write of the
                    // main handler that should be the untyped WriteObject (unless this is a stupid union type with a single type
                    // that is the same as the property's type).
                    mainHandler.GenerateWrite( write, fieldName );
                }
                else
                {
                    info.AllHandlers[0].GenerateWrite( write, fieldName );
                }

                read.Append( "case " ).AppendSourceString( p.PropertyName ).Append( ": " )
                    .OpenBlock();

                if( info.IsJsonUnionType )
                {
                    read.Append( "if( r.TokenType == System.Text.Json.JsonTokenType.Null )" );
                    if( p.IsNullable )
                    {
                        read.OpenBlock()
                            .Append( fieldName ).Append( " = null;" ).NewLine()
                            .Append( "r.Read();" )
                            .CloseBlock()
                            .Append( "else" )
                            .OpenBlock();
                    }
                    else
                    {
                        read.Append( " throw new System.Text.Json.JsonException(\"" ).Append( p.ToString()! ).Append( " cannot be null.\");" ).NewLine();
                    }

                    if( info.IsJsonUnionType )
                    {
                        read.Append( "if( r.TokenType != System.Text.Json.JsonTokenType.StartArray ) throw new System.Text.Json.JsonException( \"Expecting Json Type array.\" );" ).NewLine()
                            .Append( "r.Read();" ).NewLine()
                            .Append( "string name = r.GetString();" ).NewLine()
                            .Append( "r.Read();" ).NewLine()
                            .Append( "switch( name )" )
                            .OpenBlock();
                        foreach( var h in info.AllHandlers )
                        {
                            read.Append( "case " ).AppendSourceString( h.JsonName ).Append( ":" ).NewLine();
                            if( h.HasECMAScriptStandardJsonName && info.ECMAStandardHandlers.Contains( h ) )
                            {
                                read.Append( "case " ).AppendSourceString( h.ECMAScriptStandardJsonName.Name ).Append( ":" ).NewLine();
                            }
                            h.GenerateRead( read, fieldName, true );
                            read.Append( "break;" ).NewLine();
                        }
                        read.Append( "default: throw new System.Text.Json.JsonException( $\"Unknown type name '{name}'.\" );" )
                            .CloseBlock()
                            .Append( "if( r.TokenType != System.Text.Json.JsonTokenType.EndArray ) throw new System.Text.Json.JsonException( \"Expecting end of Json Type array.\" );" ).NewLine()
                            .Append( "r.Read();" );
                   }
                    else
                    {
                        info.AllHandlers[0].GenerateRead( read, fieldName, false );
                    }
                    if( p.IsNullable )
                    {
                        read.CloseBlock();
                    }

                }
                else
                {
                    info.AllHandlers[0].GenerateRead( read, fieldName, false );
                }
                read.Append( "break; " )
                    .CloseBlock();
            } );
            return info;
        }

        /// <summary>
        /// Generates the "public void Read( ref System.Text.Json.Utf8JsonReader r )" method
        /// that handles a potential array definition with a check of the type and the loop
        /// over the properties: the returned part must be filled with the case statements on
        /// the property names.
        /// </summary>
        /// <param name="pocoInfo">The poco root information.</param>
        /// <param name="pocoClass">The target class to generate.</param>
        /// <returns>A header part and the part in the switch statement.</returns>
        static (ITypeScopePart,ITypeScopePart) GenerateReadBody( IPocoRootInfo pocoInfo, ITypeScope pocoClass )
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
if( r.TokenType != System.Text.Json.JsonTokenType.StartObject ) throw new System.Text.Json.JsonException( ""Expecting '{' to start a Poco."" );
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
            r.Skip();
            if( t == System.Text.Json.JsonTokenType.StartArray || t == System.Text.Json.JsonTokenType.StartObject ) r.Read();
            break;
        }
    }
}
if( r.TokenType != System.Text.Json.JsonTokenType.EndObject ) throw new System.Text.Json.JsonException( ""Expecting '}' to end a Poco."" );
r.Read();
if( isDef )
{
    if( r.TokenType != System.Text.Json.JsonTokenType.EndArray ) throw new System.Text.Json.JsonException( ""Expecting ']' to end a Poco array."" );
    r.Read();
}
" ).CloseBlock();
            return (readHeader,read);
        }

        // Step 3: Calls JsonSerializationCodeGen.FinalizeCodeGeneration that will generate
        //         the global Read & Write of "untyped" objects method. 
        void FinalizeJsonSupport( IActivityMonitor monitor, JsonSerializationCodeGen jsonCodeGen )
        {
            if( jsonCodeGen.FinalizeCodeGeneration( monitor ) )
            {
                foreach( var a in _finalReadWrite ) a();
            }
        }



    }
}
