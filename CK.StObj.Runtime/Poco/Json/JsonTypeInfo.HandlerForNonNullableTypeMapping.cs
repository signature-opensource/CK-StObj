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
            public bool IsNullable => _mapping.IsNullable;
            public Type Type => _nullable.Type;
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

            public void GenerateWrite( ICodeWriter write, string variableName, bool? withType = null ) => _mapping.GenerateWrite( write, variableName, withType );

            public void GenerateRead( ICodeWriter read, string variableName, bool assignOnly ) => _mapping.GenerateRead( read, variableName, assignOnly );

            public IJsonCodeGenHandler ToNullHandler() => this;

            public IJsonCodeGenHandler ToNonNullHandler() => _nullable;
        }
    }
}
