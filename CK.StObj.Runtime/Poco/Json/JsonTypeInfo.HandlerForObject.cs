using CK.CodeGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CK.Setup.Json
{
    public partial class JsonTypeInfo
    {
        internal class HandlerForObjectMapping : IJsonCodeGenHandler
        {
            public JsonTypeInfo TypeInfo => JsonTypeInfo.Untyped;
            public bool IsNullable => true; // Always true.
            public Type Type { get; }
            public string JsonName => TypeInfo.JsonName;
            public IEnumerable<string> PreviousJsonNames => TypeInfo.PreviousJsonNames;
            public ECMAScriptStandardJsonName ECMAScriptStandardJsonName => TypeInfo.ECMAScriptStandardJsonName;

            public bool IsTypeMapping => Type != typeof(object);

            public HandlerForObjectMapping( Type t )
            {
                Type = t;
            }

            public void GenerateWrite( ICodeWriter write, string variableName, bool? withType = null )
            {
                write.Append( "PocoDirectory_CK.WriteObject( w, " ).Append( variableName ).Append( ", options );" ).NewLine();
            }

            public void GenerateRead( ICodeWriter read, string variableName, bool assignOnly )
            {
                read.Append( variableName ).Append( " = (" ).AppendCSharpName( Type ).Append( ")PocoDirectory_CK.ReadObject( ref r, options );" ).NewLine();
            }

            public IJsonCodeGenHandler ToNullHandler() => this;

            public IJsonCodeGenHandler ToNonNullHandler() => this;
        }
    }
}
