using CK.CodeGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CK.Setup.Json
{
    public partial class JsonTypeInfo
    {
        internal class HandlerForNonNullableTypeMapping : JsonCodeGenHandler
        {
            public override JsonTypeInfo TypeInfo => _mapping.TypeInfo;
            public override NullableTypeTree Type => _nullable.Type.ToAbnormalNull();
            public override string GenCSharpName => _nullable.GenCSharpName;

            public override string JsonName => _mapping.JsonName;
            public override IEnumerable<string> PreviousJsonNames => _mapping.PreviousJsonNames;
            public override ECMAScriptStandardJsonName ECMAScriptStandardJsonName => _mapping.ECMAScriptStandardJsonName;
            public override JsonCodeGenHandler? TypeMapping => _mapping;

            readonly JsonCodeGenHandler _mapping;
            readonly HandlerForTypeMapping _nullable;

            public HandlerForNonNullableTypeMapping( HandlerForTypeMapping nullable )
            {
                Debug.Assert( nullable.TypeMapping != null && nullable.IsNullable );
                _nullable = nullable;
                _mapping = _nullable.TypeMapping.ToNonNullHandler();
            }

            public override void GenerateWrite( ICodeWriter write, string variableName, bool? withType = null )
            {
                _mapping.GenerateWrite( write, variableName, withType );
            }

            public override void GenerateRead( ICodeWriter read, string variableName, bool assignOnly )
            {
                if( _mapping.TypeInfo == JsonTypeInfo.ObjectType )
                {
                    read.Append( variableName ).Append( " = (" ).Append( GenCSharpName ).Append( ")PocoDirectory_CK.ReadObject( ref r, options, true );" ).NewLine();
                }
                else
                {
                    _mapping.GenerateRead( read, variableName, assignOnly );
                }
            }


            public override JsonCodeGenHandler ToNullHandler() => this;

            public override JsonCodeGenHandler ToNonNullHandler() => _nullable;
        }
    }
}
