using CK.CodeGen;
using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization.Metadata;
using static CK.Core.PocoJsonExportSupport;

namespace CK.Setup.PocoJson
{
    /// <summary>
    /// The code reader delegate is in charge of generating the write code from a <see cref="System.Text.Json.Utf8JsonReader"/>
    /// variable named "r" into a "ref variable" and a PocoJsonImportOptions variable named "options".
    /// </summary>
    /// <param name="write">The code writer to uses.</param>
    /// <param name="variableName">The variable name to read.</param>
    delegate void CodeReader( ICodeWriter write, string variableName );

    sealed partial class ImportCodeGenerator
    {
        readonly ITypeScope _importerType;
        readonly ExchangeableTypeNameMap _nameMap;
        readonly ICSCodeGenerationContext _generationContext;
        readonly CodeReader[] _readers;

        public ImportCodeGenerator( ITypeScope importerType, ExchangeableTypeNameMap nameMap, ICSCodeGenerationContext generationContext )
        {
            _importerType = importerType;
            _nameMap = nameMap;
            _generationContext = generationContext;
            _readers = new CodeReader[nameMap.TypeSystem.AllNonNullableTypes.Count];
        }

        void GenerateRead( ICodeWriter writer, IPocoType t, string variableName )
        {
            if( t.Type.IsValueType )
            {
                if( t.IsNullable )
                {
                    writer.Append( "if(r.TokenType==System.Text.Json.JsonTokenType.Null)" )
                          .OpenBlock()
                          .Append( variableName ).Append( "=default;" ).NewLine()
                          .Append( "r.Read();" )
                          .CloseBlock()
                          .Append( "else" )
                          .OpenBlock();
                    _readers[t.Index >> 1].Invoke( writer, variableName );
                    writer.CloseBlock();
                }
                else
                {
                    _readers[t.Index >> 1].Invoke( writer, variableName );
                }
            }
            else
            {
                // Since we are working in oblivious mode, any reference type MAY be null.
                writer.Append( "if(" ).Append( variableName ).Append( "== null) w.WriteNullValue();" ).NewLine()
                      .Append( "else" )
                      .OpenBlock();
                _readers[t.Index >> 1].Invoke( writer, variableName );
                writer.CloseBlock();
            }
        }



        public bool Run( IActivityMonitor monitor )
        {
            return true;
        }

    }
}
