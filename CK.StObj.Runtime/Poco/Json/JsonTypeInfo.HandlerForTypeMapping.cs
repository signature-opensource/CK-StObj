using CK.CodeGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CK.Setup.Json
{
    public partial class JsonTypeInfo
    {
        internal class HandlerForTypeMapping : IJsonCodeGenHandler
        {
            public JsonTypeInfo TypeInfo => _mapping.TypeInfo;
            public bool IsNullable => _mapping.IsNullable;
            public Type Type { get; }
            public string JsonName => _mapping.JsonName;
            public IEnumerable<string> PreviousJsonNames => _mapping.PreviousJsonNames;
            public ECMAScriptStandardJsonName ECMAScriptStandardJsonName => _mapping.ECMAScriptStandardJsonName;
            public IJsonCodeGenHandler? TypeMapping => _mapping;

            readonly IJsonCodeGenHandler _mapping;
            readonly HandlerForNonNullableTypeMapping _nonNullable;

            public HandlerForTypeMapping( IJsonCodeGenHandler mapping, Type t )
            {
                Debug.Assert( mapping is HandlerForReferenceType );
                _mapping = mapping;
                Type = t;
                _nonNullable = new HandlerForNonNullableTypeMapping( this );
            }

            public void GenerateWrite( ICodeWriter write, string variableName, bool? withType = null ) => _mapping.GenerateWrite( write, variableName, withType );

            public void GenerateRead( ICodeWriter read, string variableName, bool assignOnly ) => _mapping.GenerateRead( read, variableName, assignOnly );

            public IJsonCodeGenHandler ToNullHandler() => this;

            public IJsonCodeGenHandler ToNonNullHandler() => _nonNullable;
        }
    }
}
