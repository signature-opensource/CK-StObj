using CK.CodeGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CK.Setup.Json
{
    public partial class JsonTypeInfo
    {
        internal class HandlerForValueType : JsonCodeGenHandler
        {
            public override JsonTypeInfo TypeInfo { get; }

            readonly HandlerForNullableValueType _nullHandler;

            public HandlerForValueType( JsonTypeInfo info )
            {
                TypeInfo = info;
                _nullHandler = new HandlerForNullableValueType( this );
            }

            public override void GenerateWrite( ICodeWriter write, string variableName, bool? withType = null )
            {
                Debug.Assert( TypeInfo.IsFinal );
                DoGenerateWrite( write, variableName, handleNull: false, withType ?? false );
            }

            public override void GenerateRead( ICodeWriter read, string variableName, bool assignOnly )
            {
                Debug.Assert( TypeInfo.IsFinal );
                DoGenerateRead( read, variableName, assignOnly );
            }

            public override JsonCodeGenHandler ToNullHandler() => _nullHandler;

            public override JsonCodeGenHandler ToNonNullHandler() => this;
        }
    }
}
