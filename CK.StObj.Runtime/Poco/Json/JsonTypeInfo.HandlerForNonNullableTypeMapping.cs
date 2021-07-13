using CK.CodeGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CK.Setup.Json
{
    public partial class JsonTypeInfo
    {
        internal class HandlerForNonNullableTypeMapping : IJsonCodeGenHandler
        {
            public JsonTypeInfo TypeInfo => _mapping.TypeInfo;
            public bool IsNullable => false;
            public NullableTypeTree Type => _nullable.Type.ToAbnormalNull();
            public string GenCSharpName => _nullable.GenCSharpName;

            public string JsonName => _mapping.JsonName;
            public IEnumerable<string> PreviousJsonNames => _mapping.PreviousJsonNames;
            public ECMAScriptStandardJsonName ECMAScriptStandardJsonName => _mapping.ECMAScriptStandardJsonName;
            public IJsonCodeGenHandler? TypeMapping => _mapping;

            readonly IJsonCodeGenHandler _mapping;
            readonly HandlerForTypeMapping _nullable;

            public HandlerForNonNullableTypeMapping( HandlerForTypeMapping nullable )
            {
                Debug.Assert( nullable.TypeMapping != null && nullable.IsNullable );
                _nullable = nullable;
                _mapping = _nullable.TypeMapping.ToNonNullHandler();
            }

            public void GenerateWrite( ICodeWriter write, string variableName, bool? withType = null )
            {
                _mapping.GenerateWrite( write, variableName, withType );
            }

            public void GenerateRead( ICodeWriter read, string variableName, bool assignOnly )
            {
                if( _mapping.TypeInfo == JsonTypeInfo.ObjectType )
                {
                    read.Append( "if( r.TokenType == System.Text.Json.JsonTokenType.Null ) throw new System.Text.Json.JsonException(\"Unexpected null value.\");" ).NewLine();
                    read.Append( variableName ).Append( " = (" ).Append( GenCSharpName ).Append( ")PocoDirectory_CK.ReadObject( ref r, options );" ).NewLine();
                }
                else
                {
                    _mapping.GenerateRead( read, variableName, assignOnly );
                }
            }


            public IJsonCodeGenHandler ToNullHandler() => this;

            public IJsonCodeGenHandler ToNonNullHandler() => _nullable;
        }
    }
}
