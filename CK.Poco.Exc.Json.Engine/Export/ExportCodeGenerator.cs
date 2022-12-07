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
    /// <param name="writer">The code writer to uses.</param>
    /// <param name="variableName">The variable name to write.</param>
    delegate void CodeWriter( ICodeWriter writer, string variableName );

    sealed partial class ExportCodeGenerator
    {
        readonly ITypeScope _exporterType;
        readonly ExchangeableTypeNameMap _nameMap;
        readonly ExchangeableTypeNameMap _simplifieldNameMap;
        readonly ICSCodeGenerationContext _generationContext;
        // Writers are for the non nullable types, whether they are oblivious types
        // or not: writers for the same "oblivious family" will share the same function.
        readonly CodeWriter[] _writers;

        public ExportCodeGenerator( ITypeScope exporterType,
                                    ExchangeableTypeNameMap nameMap,
                                    ExchangeableTypeNameMap simplifieldNameMap,
                                    ICSCodeGenerationContext generationContext )
        {
            _exporterType = exporterType;
            _nameMap = nameMap;
            _simplifieldNameMap = simplifieldNameMap;
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

        // Used by GenerateWriteMethods.GeneratePocoWriteMethod for the WriteJson( w, withType, options )
        // and  GenerateWriteAny().
        void GenerateTypeHeader( ICodeWriter writer, IPocoType nonNullable, bool honorOption )
        {
            var typeName = _nameMap.GetName( nonNullable );
            var simplifiedTypeName = _simplifieldNameMap.GetName( nonNullable );

            if( honorOption ) writer.Append( $"if(!options.TypeLess)" );
            if( typeName.Name != simplifiedTypeName.Name )
            {
                writer.Append( "w.WriteStringValue(options.UseSimplifiedTypes?" )
                    .AppendSourceString( simplifiedTypeName.Name )
                    .Append( ":" );
            }
            else
            {
                writer.Append( "w.WriteStringValue(" );
            }
            writer.AppendSourceString( typeName.Name ).Append( ");" ).NewLine();
        }

        public bool Run( IActivityMonitor monitor )
        {
            RegisterWriters();
            GenerateWriteMethods( monitor );
            GenerateWriteAny();
            return true;
        }
    }
}
