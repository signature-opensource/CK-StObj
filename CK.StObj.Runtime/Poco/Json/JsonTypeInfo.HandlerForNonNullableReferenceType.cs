using CK.CodeGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CK.Setup.Json
{
    public partial class JsonTypeInfo
    {
        internal class HandlerForNonNullableReferenceType : IJsonCodeGenHandler
        {
            public JsonTypeInfo TypeInfo => _nullHandler.TypeInfo;
            public bool IsNullable => false; // Always false.
            public NullableTypeTree Type => TypeInfo.Type.ToAbnormalNull();
            public string GenCSharpName => TypeInfo.GenCSharpName;
            public string JsonName => TypeInfo.NonNullableJsonName;
            public IEnumerable<string> PreviousJsonNames => TypeInfo.NonNullablePreviousJsonNames;
            public ECMAScriptStandardJsonName ECMAScriptStandardJsonName => TypeInfo.NonNullableECMAScriptStandardJsonName;

            public IJsonCodeGenHandler? TypeMapping => null;

            readonly HandlerForReferenceType _nullHandler;

            public HandlerForNonNullableReferenceType( HandlerForReferenceType nullHandler )
            {
                _nullHandler = nullHandler;
            }

            public void GenerateWrite( ICodeWriter write, string variableName, bool? withType = null )
            {
                if( TypeInfo == JsonTypeInfo.ObjectType || (withType == null && !TypeInfo.IsFinal) )
                {
                    if( !IsNullable )
                    {
                        write.Append( "if( " ).Append( variableName ).Append( " == null ) throw new InvalidOperationException(\"A null value appear where it should not. Writing JSON is impossible.\");" ).NewLine();
                    }
                    write.Append( "PocoDirectory_CK.WriteObject( w, " ).Append( variableName ).Append( ", options );" ).NewLine();
                }
                else
                {
                    this.DoGenerateWrite( write, variableName, handleNull: true, withType ?? false );
                }
            }

            public void GenerateRead( ICodeWriter read, string variableName, bool assignOnly )
            {
                if( !TypeInfo.IsFinal )
                {
                    read.Append( "if( r.TokenType == System.Text.Json.JsonTokenType.Null ) throw new System.Text.Json.JsonException(\"Unexpected null value.\");" ).NewLine();
                    read.Append( variableName ).Append( " = (" ).Append( GenCSharpName ).Append( ")PocoDirectory_CK.ReadObject( ref r, options );" ).NewLine();
                }
                else
                {
                    this.DoGenerateRead( read, variableName, assignOnly );
                }
            }

            public IJsonCodeGenHandler ToNullHandler() => _nullHandler;

            public IJsonCodeGenHandler ToNonNullHandler() => this;
        }
    }
}
