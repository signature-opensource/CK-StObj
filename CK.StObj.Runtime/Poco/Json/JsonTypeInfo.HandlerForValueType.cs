using CK.CodeGen;
using System;
using System.Diagnostics;

namespace CK.Setup.Json
{
    public partial class JsonTypeInfo
    {
        class HandlerForValueType : IJsonCodeGenHandler
        {
            public JsonTypeInfo TypeInfo { get; }
            public bool IsNullable => false; // Always false.
            public Type Type => TypeInfo.Type;
            public string JsonName => TypeInfo.JsonName;
            public bool IsTypeMapping => false;

            readonly HandlerForNullableValueType _nullHandler;

            public HandlerForValueType( JsonTypeInfo info )
            {
                TypeInfo = info;
                _nullHandler = new HandlerForNullableValueType( this );
            }

            public void GenerateWrite( ICodeWriter write, string variableName, bool? withType = null )
            {
                Debug.Assert( TypeInfo.IsFinal );
                TypeInfo.GenerateWrite( write, variableName, false, withType ?? false );
            }

            public void GenerateRead( ICodeWriter read, string variableName, bool assignOnly )
            {
                Debug.Assert( TypeInfo.IsFinal );
                TypeInfo.GenerateRead( read, variableName, assignOnly, false );
            }

            public IJsonCodeGenHandler ToNullHandler() => _nullHandler;

            public IJsonCodeGenHandler ToNonNullHandler() => this;
        }
    }
}
