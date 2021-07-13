using CK.CodeGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CK.Setup.Json
{
    public partial class JsonTypeInfo
    {
        class HandlerForNullableValueType : IJsonCodeGenHandler
        {
            public JsonTypeInfo TypeInfo => _nonNullHandler.TypeInfo;
            public bool IsNullable => true;
            public NullableTypeTree Type => _nonNullHandler.Type.ToAbnormalNull();
            public string GenCSharpName => TypeInfo.GenCSharpName + '?';

            public string JsonName => TypeInfo.NonNullableJsonName + '?';
            public IEnumerable<string> PreviousJsonNames => TypeInfo.NonNullablePreviousJsonNames.Select( n => n + '?' );
            // It must be dynamic since the ECMAScriptStandard name is set after the initialization.
            // The nullable reference is canonical since there cannot be a nullable and a non nullable of the same type
            // in an union type (the non nullable one has been removed).
            public ECMAScriptStandardJsonName ECMAScriptStandardJsonName => new( TypeInfo.NonNullableECMAScriptStandardJsonName.Name + '?', TypeInfo.NonNullableECMAScriptStandardJsonName.IsCanonical );

            public IJsonCodeGenHandler? TypeMapping => null;

            readonly HandlerForValueType _nonNullHandler;

            internal HandlerForNullableValueType( HandlerForValueType h )
            {
                _nonNullHandler = h;
            }

            public void GenerateWrite( ICodeWriter write, string variableName, bool? withType = null )
            {
                Debug.Assert( TypeInfo.IsFinal );
                this.DoGenerateWrite( write, variableName, true, withType ?? false );
            }

            public void GenerateRead( ICodeWriter read, string variableName, bool assignOnly )
            {
                Debug.Assert( TypeInfo.IsFinal );
                this.DoGenerateRead( read, variableName, assignOnly );
            }

            public IJsonCodeGenHandler ToNullHandler() => this;

            public IJsonCodeGenHandler ToNonNullHandler() => _nonNullHandler;
        }
    }
}
