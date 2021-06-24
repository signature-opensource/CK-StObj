using CK.CodeGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CK.Setup.Json
{
    public partial class JsonTypeInfo
    {
        internal class HandlerForUnambiguousMapping : IJsonCodeGenHandler
        {
            public JsonTypeInfo TypeInfo => _mapping.TypeInfo;
            public bool IsNullable => _mapping.IsNullable;
            public Type Type { get; }
            public string JsonName => _mapping.JsonName;
            public IEnumerable<string> PreviousJsonNames => _mapping.PreviousJsonNames;
            public ECMAScriptStandardJsonName ECMAScriptStandardJsonName => _mapping.ECMAScriptStandardJsonName;
            public bool IsTypeMapping => true;

            readonly IJsonCodeGenHandler _mapping;

            public HandlerForUnambiguousMapping( IJsonCodeGenHandler mapping, Type t )
            {
                _mapping = mapping;
                Type = t;
            }

            public void GenerateWrite( ICodeWriter write, string variableName, bool? withType = null ) => _mapping.GenerateWrite( write, variableName, withType );

            public void GenerateRead( ICodeWriter read, string variableName, bool assignOnly ) => _mapping.GenerateRead( read, variableName, assignOnly );

            public IJsonCodeGenHandler ToNullHandler() => this;

            public IJsonCodeGenHandler ToNonNullHandler() => this;
        }
    }
}
