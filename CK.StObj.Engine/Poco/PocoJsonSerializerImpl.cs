using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

#nullable enable

namespace CK.Setup
{
    /// <summary>
    /// Implements the Json serialization. This class extends the Poco classes to support
    /// the API exposed as extension methods by the CK.Poco.PocoJsonSerializer static
    /// class (in CK.Poco.Json).
    /// </summary>
    public partial class PocoJsonSerializerImpl : ICodeGenerator
    {
        IActivityMonitor? _monitor;
        ITypeScope? _pocoDirectory;

        IActivityMonitor Monitor => _monitor!;

        ITypeScope PocoDirectory => _pocoDirectory!;

        /// <summary>
        /// Extends PocoDirectory_CK, the factories and the Poco classes.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="c">The code generation context.</param>
        /// <returns>Always <see cref="AutoImplementationResult.Success"/>.</returns>
        public AutoImplementationResult Implement( IActivityMonitor monitor, ICodeGenerationContext c )
        {
            _monitor = monitor;
            _pocoDirectory = c.Assembly.FindOrCreateAutoImplementedClass( monitor, typeof( PocoDirectory ) );

            var pocoSupport = c.Assembly.GetPocoSupportResult();
            if( pocoSupport == null )
            {
                monitor.Info( "No Poco support available. Skipping Poco serialization code generation." );
                return AutoImplementationResult.Success;
            }

            InitializeMap();
            // IPoco and IClosedPoco are not in the "OtherInterfaces".
            AddUntypedHandler( typeof( IPoco ) );
            AddUntypedHandler( typeof( IClosedPoco ) );

            // Registers TypeInfo for the PocoClass and maps its interfaces to the PocoClass.
            foreach( var root in pocoSupport.Roots )
            {
                var typeInfo = AddTypeInfo( root.PocoClass, root.Name, root.PreviousNames ).Configure( 
                                ( ICodeWriter write, string variableName )
                                        => write.Append( variableName ).Append( ".Write( w, false );" ),
                                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                                {
                                    if( !assignOnly )
                                    {
                                        if( isNullable )
                                        {
                                            read.Append( "if( " ).Append( variableName ).Append( " != null ) " ).NewLine();
                                        }
                                        read.Append( "((" ).AppendCSharpName( root.PocoClass ).Append( ')' ).Append( variableName ).Append( ')' ).Append( ".Read( ref r );" );
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
                                            .Append( "( ref r );" );
                                    }
                                } );
                foreach( var i in root.Interfaces )
                {
                    AddTypeHandlerAlias( i.PocoInterface, typeInfo.NullHandler );
                }
            }
            // Maps the "other Poco interfaces" to "Untyped" object.
            foreach( var other in pocoSupport.OtherInterfaces )
            {
                AddUntypedHandler( other.Key );
            }

            // Generates the factory and the Poco class code.
            foreach( var root in pocoSupport.Roots )
            {
                var factory = c.Assembly.FindOrCreateAutoImplementedClass( monitor, root.PocoFactoryClass );
                foreach( var i in root.Interfaces )
                {
                    var interfaceName = i.PocoInterface.ToCSharpName();
                    var readerName = "PocoJsonSerializer.IFactoryReader<" + interfaceName + ">";

                    factory.Definition.BaseTypes.Add( new ExtendedTypeName( readerName ) );
                    factory.Append( interfaceName ).Append( ' ' ).Append( readerName ).Append( ".Read( ref System.Text.Json.Utf8JsonReader r )" ).NewLine()
                            .Append( " => r.TokenType == System.Text.Json.JsonTokenType.Null ? null : new " )
                            .Append( root.PocoClass.Name ).Append( "( ref r );" ).NewLine();

                }
                factory.Append( "public IPoco ReadTyped( ref System.Text.Json.Utf8JsonReader r ) => new " ).Append( root.PocoClass.Name ).Append( "( ref r );" ).NewLine();

                var pocoClass = c.Assembly.FindOrCreateAutoImplementedClass( monitor, root.PocoClass );

                // Generates the Poco class Read and Write methhods.
                // UnionTypes on properties are registered.
                ExtendPocoClass( root, pocoClass );
            }

            // Generates the code for "dynamic"/"untyped" object.
            FillDynamicMaps( _map );
            GenerateObjectWrite();
            GenerateObjectRead();

            return AutoImplementationResult.Success;
        }

        void ExtendPocoClass( IPocoRootInfo pocoInfo, ITypeScope pocoClass )
        {
            // Each Poco class is a IWriter and has a constructor that accepts a Utf8JsonReader.
            pocoClass.Definition.BaseTypes.Add( new ExtendedTypeName( "CK.Core.PocoJsonSerializer.IWriter" ) );
            pocoClass.Append( "public void Write( System.Text.Json.Utf8JsonWriter w, bool withType )" )
                 .OpenBlock()
                 .Append( "if( withType ) { w.WriteStartArray(); w.WriteStringValue( " ).AppendSourceString( pocoInfo.Name ).Append( "); }" ).NewLine()
                 .Append( "w.WriteStartObject();" ).NewLine();
            var write = pocoClass.CreatePart();
            pocoClass.NewLine()
                 .Append( "w.WriteEndObject();" ).NewLine()
                 .Append( "if( withType ) w.WriteEndArray();" ).NewLine()
                 .CloseBlock();

            pocoClass.Append( "public " ).Append( pocoClass.Name ).Append( "( ref System.Text.Json.Utf8JsonReader r ) : this()" )
                 .OpenBlock()
                 .Append( "Read( ref r );" )
                 .CloseBlock();

            // Poco has a Read method but it is not (currently) exposed.
            ITypeScopePart read = GenerateReadBody( pocoInfo, pocoClass );

            foreach( var p in pocoInfo.PropertyList )
            {
                foreach( var union in p.PropertyUnionTypes )
                {
                    TryFindOrCreateHandler( union );
                }

                write.Append( "w.WritePropertyName( " ).AppendSourceString( p.PropertyName ).Append( " );" ).NewLine();

                var handler = TryFindOrCreateHandler( p.PropertyType );
                if( handler == null ) continue;
                // If its an AutoInstantiated property with no setter, it cannot be null.
                if( handler.IsNullable && p.AutoInstantiated && !p.HasDeclaredSetter )
                {
                    handler = handler.Info.NotNullHandler;
                }
                handler.GenerateWrite( write, p.PropertyName );

                read.Append( "case " ).AppendSourceString( p.PropertyName ).Append( " : " )
                    .OpenBlock();
                handler.GenerateRead( read, p.PropertyName, false );
                read.Append( "break; " )
                    .CloseBlock();
            }
        }

        /// <summary>
        /// Generates the "public void Read( ref System.Text.Json.Utf8JsonReader r )" method
        /// that handles a potential array definition with a check of the type and the loop
        /// over the properties: the returned part must be filled with the case statements on
        /// the property names.
        /// </summary>
        /// <param name="pocoInfo">The poco root information.</param>
        /// <param name="pocoClass">The target class to generate.</param>
        /// <returns>The part in the switch statement.</returns>
        ITypeScopePart GenerateReadBody( IPocoRootInfo pocoInfo, ITypeScope pocoClass )
        {
            pocoClass.Append( "public void Read( ref System.Text.Json.Utf8JsonReader r )" )
              .OpenBlock()
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
            return read;
        }

    }
}
