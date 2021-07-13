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
            public bool IsNullable => true;
            public NullableTypeTree Type { get; }
            public string GenCSharpName { get; }

            public string JsonName => _mapping.JsonName;
            public IEnumerable<string> PreviousJsonNames => _mapping.PreviousJsonNames;
            public ECMAScriptStandardJsonName ECMAScriptStandardJsonName => _mapping.ECMAScriptStandardJsonName;
            public IJsonCodeGenHandler? TypeMapping => _mapping;

            readonly IJsonCodeGenHandler _mapping;
            readonly HandlerForNonNullableTypeMapping _nonNullable;

            public HandlerForTypeMapping( IJsonCodeGenHandler mapping, NullableTypeTree t )
            {
                Debug.Assert( mapping is HandlerForReferenceType && mapping.IsNullable );
                _mapping = mapping;
                Type = t;
                GenCSharpName = t.Type.ToCSharpName();
                _nonNullable = new HandlerForNonNullableTypeMapping( this );
            }

            public void GenerateWrite( ICodeWriter write, string variableName, bool? withType = null )
            {
                _mapping.GenerateWrite( write, variableName, withType );
            }

            public void GenerateRead( ICodeWriter read, string variableName, bool assignOnly )
            {
                if( _mapping.TypeInfo == JsonTypeInfo.ObjectType )
                {
                    read.Append( variableName ).Append( " = (" ).Append( GenCSharpName ).Append( ")PocoDirectory_CK.ReadObject( ref r, options );" ).NewLine();
                }
                else
                {
                    _mapping.GenerateRead( read, variableName, assignOnly );
                }
            }

            public IJsonCodeGenHandler ToNullHandler() => this;

            public IJsonCodeGenHandler ToNonNullHandler() => _nonNullable;
        }
    }
}
