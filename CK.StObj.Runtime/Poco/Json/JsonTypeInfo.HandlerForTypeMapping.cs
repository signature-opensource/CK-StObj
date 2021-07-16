using CK.CodeGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CK.Setup.Json
{
    public partial class JsonTypeInfo
    {
        internal class HandlerForTypeMapping : JsonCodeGenHandler
        {
            public override JsonTypeInfo TypeInfo => _mapping.TypeInfo;
            public override bool IsNullable => true;
            public override NullableTypeTree Type { get; }
            public override string GenCSharpName { get; }

            public override string JsonName => _mapping.JsonName;
            public override IEnumerable<string> PreviousJsonNames => _mapping.PreviousJsonNames;
            public override ECMAScriptStandardJsonName ECMAScriptStandardJsonName => _mapping.ECMAScriptStandardJsonName;
            public override JsonCodeGenHandler? TypeMapping => _mapping;

            readonly JsonCodeGenHandler _mapping;
            readonly HandlerForNonNullableTypeMapping _nonNullable;

            public HandlerForTypeMapping( JsonCodeGenHandler mapping, NullableTypeTree t )
            {
                Debug.Assert( mapping is HandlerForReferenceType && mapping.IsNullable );
                _mapping = mapping;
                Type = t;
                GenCSharpName = t.Type.ToCSharpName();
                _nonNullable = new HandlerForNonNullableTypeMapping( this );
            }

            public override void GenerateWrite( ICodeWriter write, string variableName, bool? withType = null )
            {
                _mapping.GenerateWrite( write, variableName, withType );
            }

            public override void GenerateRead( ICodeWriter read, string variableName, bool assignOnly )
            {
                if( _mapping.TypeInfo == JsonTypeInfo.ObjectType )
                {
                    read.Append( variableName ).Append( " = (" ).Append( GenCSharpName ).Append( ")PocoDirectory_CK.ReadObject( ref r, options, false );" ).NewLine();
                }
                else
                {
                    _mapping.GenerateRead( read, variableName, assignOnly );
                }
            }

            public override JsonCodeGenHandler ToNullHandler() => this;

            public override JsonCodeGenHandler ToNonNullHandler() => _nonNullable;
        }
    }
}
