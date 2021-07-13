using CK.CodeGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CK.Setup.Json
{
    public partial class JsonTypeInfo
    {
        internal class HandlerForValueType : IJsonCodeGenHandler
        {
            public JsonTypeInfo TypeInfo { get; }
            public bool IsNullable => false; // Always false.
            public NullableTypeTree Type => TypeInfo.Type;
            public string GenCSharpName => TypeInfo.GenCSharpName;

            public string JsonName => TypeInfo.NonNullableJsonName;
            public IEnumerable<string> PreviousJsonNames => TypeInfo.NonNullablePreviousJsonNames;
            public ECMAScriptStandardJsonName ECMAScriptStandardJsonName => TypeInfo.NonNullableECMAScriptStandardJsonName;
            public IJsonCodeGenHandler? TypeMapping => null;

            readonly HandlerForNullableValueType _nullHandler;

            public HandlerForValueType( JsonTypeInfo info )
            {
                TypeInfo = info;
                _nullHandler = new HandlerForNullableValueType( this );
            }

            public void GenerateWrite( ICodeWriter write, string variableName, bool? withType = null )
            {
                Debug.Assert( TypeInfo.IsFinal );
                this.DoGenerateWrite( write, variableName, handleNull: false, withType ?? false );
            }

            public void GenerateRead( ICodeWriter read, string variableName, bool assignOnly )
            {
                Debug.Assert( TypeInfo.IsFinal );
                this.DoGenerateRead( read, variableName, assignOnly );
            }

            public IJsonCodeGenHandler ToNullHandler() => _nullHandler;

            public IJsonCodeGenHandler ToNonNullHandler() => this;
        }
    }
}
