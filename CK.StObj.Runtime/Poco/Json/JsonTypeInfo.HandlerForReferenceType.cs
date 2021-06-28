using CK.CodeGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CK.Setup.Json
{
    public partial class JsonTypeInfo
    {
        internal class HandlerForReferenceType : IJsonCodeGenHandler
        {
            public JsonTypeInfo TypeInfo { get; }
            public bool IsNullable => true; // Always true.
            public Type Type => TypeInfo.Type;
            public string JsonName => TypeInfo.JsonName + '?';
            public IEnumerable<string> PreviousJsonNames => TypeInfo.PreviousJsonNames.Select( n => n + '?' );
            public ECMAScriptStandardJsonName ECMAScriptStandardJsonName => new( TypeInfo.ECMAScriptStandardJsonName.Name + '?', TypeInfo.ECMAScriptStandardJsonName.IsCanonical );

            public IJsonCodeGenHandler? TypeMapping => null;
            readonly HandlerForNonNullableReferenceType _nonNullable;

            public HandlerForReferenceType( JsonTypeInfo info )
            {
                TypeInfo = info;
                _nonNullable = new HandlerForNonNullableReferenceType( this );
            }

            public void GenerateWrite( ICodeWriter write, string variableName, bool? withType = null )
            {
                if( TypeInfo == ObjectType || (withType == null && !TypeInfo.IsFinal) )
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
                if( TypeInfo == JsonTypeInfo.ObjectType || !TypeInfo.IsFinal )
                {
                    read.Append( variableName ).Append( " = (" ).AppendCSharpName( Type ).Append( ")PocoDirectory_CK.ReadObject( ref r, options );" ).NewLine();
                }
                else
                {
                    TypeInfo.GenerateRead( read, variableName, assignOnly, true );
                }
            }

            public IJsonCodeGenHandler ToNullHandler() => this;

            public IJsonCodeGenHandler ToNonNullHandler() => _nonNullable;
        }
    }
}
