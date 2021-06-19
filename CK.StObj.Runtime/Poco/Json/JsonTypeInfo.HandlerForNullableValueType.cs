using CK.CodeGen;
using System;
using System.Diagnostics;

namespace CK.Setup.Json
{
    public partial class JsonTypeInfo
    {
        class HandlerForNullableValueType : IJsonCodeGenHandler
        {
            public JsonTypeInfo TypeInfo => _nonNullHandler.TypeInfo;
            public bool IsNullable => true;
            public Type Type { get; }
            public string JsonName => TypeInfo.JsonName + '?';
            public bool IsTypeMapping => false;

            readonly HandlerForValueType _nonNullHandler;

            internal HandlerForNullableValueType( HandlerForValueType h )
            {
                _nonNullHandler = h;
                Type = typeof( Nullable<> ).MakeGenericType( h.Type );
            }

            public void GenerateWrite( ICodeWriter write, string variableName, bool? withType = null )
            {
                Debug.Assert( TypeInfo.IsFinal );
                TypeInfo.GenerateWrite( write, variableName, true, withType ?? false );
            }

            public void GenerateRead( ICodeWriter read, string variableName, bool assignOnly )
            {
                Debug.Assert( TypeInfo.IsFinal );
                TypeInfo.GenerateRead( read, variableName, assignOnly, true );
            }

            public IJsonCodeGenHandler ToNullHandler() => this;

            public IJsonCodeGenHandler ToNonNullHandler() => _nonNullHandler;
        }
    }
}
