using CK.CodeGen;
using CK.Core;
using System;
using System.Numerics;

namespace CK.Setup.PocoJson
{
    /// <summary>
    /// The code writer delegate is in charge of generating the write code into a <see cref="System.Text.Json.Utf8JsonWriter"/>
    /// named "w" and a PocoJsonWriteContext variable named "wCtx" from a variable.
    /// </summary>
    /// <param name="writer">The code writer to uses.</param>
    /// <param name="variableName">The variable name to write.</param>
    delegate void CodeWriter( ICodeWriter writer, string variableName );

    sealed partial class ExportCodeGenerator
    {
        readonly ITypeScope _exporterType;
        readonly ExchangeableTypeNameMap _nameMap;
        readonly ICSCodeGenerationContext _generationContext;
        // Writers are for the non nullable types, whether they are oblivious types
        // or not: writers for the same "oblivious family" will share the same function.
        readonly CodeWriter[] _writers;

        public ExportCodeGenerator( ITypeScope exporterType,
                                    ExchangeableTypeNameMap nameMap,
                                    ICSCodeGenerationContext generationContext )
        {
            _exporterType = exporterType;
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
                    var v = $"CommunityToolkit.HighPerformance.NullableExtensions.DangerousGetValueOrDefaultReference(ref {variableName})";
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

        // Used by GenerateWriteMethods.GeneratePocoWriteMethod for the WriteJson( w, withType, options )
        // and GenerateWriteAny().
        void GenerateTypeHeader( ICodeWriter writer, IPocoType nonNullable, bool honorOption )
        {
            var typeName = _nameMap.GetName( nonNullable );
            if( honorOption ) writer.Append( $"if(!wCtx.Options.TypeLess)" );
            writer.Append( "w.WriteStringValue(" ).AppendSourceString( typeName.Name ).Append( ");" ).NewLine();
        }

        public bool Run( IActivityMonitor monitor )
        {
            RegisterWriters();
            GenerateWriteMethods( monitor );
            GenerateWriteAny();
            SupportPocoDirectoryJsonExportGenerated( monitor );
            return true;
        }

        void SupportPocoDirectoryJsonExportGenerated( IActivityMonitor monitor )
        {
            ITypeScope pocoDirectory = _generationContext.Assembly.Code.Global.FindOrCreateAutoImplementedClass( monitor, typeof( PocoDirectory ) );
            pocoDirectory.Definition.BaseTypes.Add( new ExtendedTypeName( "CK.Core.IPocoDirectoryJsonExportGenerated" ) );
            var read = pocoDirectory.CreateFunction( "void CK.Core.IPocoDirectoryJsonExportGenerated.WriteAnyJson( " +
                                                        "System.Text.Json.Utf8JsonWriter w, " +
                                                        "object? o, " +
                                                        "Poco.Exc.Json.PocoJsonExportOptions? options)" );

            read.Append( "var wCtx = new CK.Poco.Exc.Json.PocoJsonWriteContext( options );" ).NewLine()
                .Append( _exporterType.FullName ).Append( ".WriteAny( w, o, wCtx );" );
        }

    }
}
