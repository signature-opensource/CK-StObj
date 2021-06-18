using CK.CodeGen;
using System;
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
            public bool IsTypeMapping => true;
            public string ECMAScriptStandardJsonName => _mapping.ECMAScriptStandardJsonName;
            public bool HasECMAScriptStandardJsonName => _mapping.HasECMAScriptStandardJsonName;

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
