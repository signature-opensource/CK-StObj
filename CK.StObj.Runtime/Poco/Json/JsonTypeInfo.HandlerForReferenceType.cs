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
            public NullableTypeTree Type => TypeInfo.Type;
            public string GenCSharpName => TypeInfo.GenCSharpName;

            public string JsonName => TypeInfo.NonNullableJsonName + '?';
            public IEnumerable<string> PreviousJsonNames => TypeInfo.NonNullablePreviousJsonNames.Select( n => n + '?' );
            public ECMAScriptStandardJsonName ECMAScriptStandardJsonName => new( TypeInfo.NonNullableECMAScriptStandardJsonName.Name + '?', TypeInfo.NonNullableECMAScriptStandardJsonName.IsCanonical );

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
                    this.DoGenerateWrite( write, variableName, handleNull: true, withType ?? false );
                }
            }

            public void GenerateRead( ICodeWriter read, string variableName, bool assignOnly )
            {
                if( !TypeInfo.IsFinal )
                {
                    read.Append( variableName ).Append( " = (" ).Append( GenCSharpName ).Append( ")PocoDirectory_CK.ReadObject( ref r, options );" ).NewLine();
                }
                else
                {
                    this.DoGenerateRead( read, variableName, assignOnly );
                }
            }

            public IJsonCodeGenHandler ToNullHandler() => this;

            public IJsonCodeGenHandler ToNonNullHandler() => _nonNullable;
        }
    }
}
