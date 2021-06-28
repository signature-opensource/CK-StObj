using CK.CodeGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CK.Setup.Json
{
    public partial class JsonTypeInfo
    {
        internal class HandlerForNonNullableReferenceType : IJsonCodeGenHandler
        {
            public JsonTypeInfo TypeInfo => _nullHandler.TypeInfo;
            public bool IsNullable => false; // Always false.
            public Type Type => TypeInfo.Type;
            public string JsonName => TypeInfo.JsonName;
            public IEnumerable<string> PreviousJsonNames => TypeInfo.PreviousJsonNames;
            public ECMAScriptStandardJsonName ECMAScriptStandardJsonName => TypeInfo.ECMAScriptStandardJsonName;

            public IJsonCodeGenHandler? TypeMapping => null;

            readonly HandlerForReferenceType _nullHandler;

            public HandlerForNonNullableReferenceType( HandlerForReferenceType nullHandler )
            {
                _nullHandler = nullHandler;
            }

            public void GenerateWrite( ICodeWriter write, string variableName, bool? withType = null )
            {
                if( TypeInfo == JsonTypeInfo.ObjectType || (withType == null && !TypeInfo.IsFinal) )
                {
                    write.Append( "PocoDirectory_CK.WriteObject( w, " ).Append( variableName ).Append( ", options );" ).NewLine();
                }
                else
                {
                    this.DoGenerateWrite( write, variableName, true, withType ?? false );
                }
            }

            public void GenerateRead( ICodeWriter read, string variableName, bool assignOnly )
            {
                bool isObject = TypeInfo == JsonTypeInfo.ObjectType;
                if( isObject || !TypeInfo.IsFinal )
                {
                    read.Append( variableName ).Append( " = (" ).AppendCSharpName( Type ).Append( ")PocoDirectory_CK.ReadObject( ref r, options );" ).NewLine();
                }
                else
                {
                    TypeInfo.GenerateRead( read, variableName, assignOnly, true );
                }
            }

            public IJsonCodeGenHandler ToNullHandler() => _nullHandler;

            public IJsonCodeGenHandler ToNonNullHandler() => this;
        }
    }
}
