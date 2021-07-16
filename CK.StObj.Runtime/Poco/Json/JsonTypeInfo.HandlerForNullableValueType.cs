using CK.CodeGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CK.Setup.Json
{
    public partial class JsonTypeInfo
    {
        class HandlerForNullableValueType : JsonCodeGenHandler
        {
            public override JsonTypeInfo TypeInfo => _nonNullHandler.TypeInfo;
            public override bool IsNullable => true;
            public override NullableTypeTree Type => _nonNullHandler.Type.ToAbnormalNull();
            public override string GenCSharpName => TypeInfo.GenCSharpName + '?';

            readonly HandlerForValueType _nonNullHandler;

            internal HandlerForNullableValueType( HandlerForValueType h )
            {
                _nonNullHandler = h;
            }

            public override void GenerateWrite( ICodeWriter write, string variableName, bool? withType = null )
            {
                Debug.Assert( TypeInfo.IsFinal );
                this.DoGenerateWrite( write, variableName, true, withType ?? false );
            }

            public override void GenerateRead( ICodeWriter read, string variableName, bool assignOnly )
            {
                DoGenerateRead( read, variableName, assignOnly );
            }

            public override JsonCodeGenHandler ToNullHandler() => this;

            public override JsonCodeGenHandler ToNonNullHandler() => _nonNullHandler;
        }
    }
}
