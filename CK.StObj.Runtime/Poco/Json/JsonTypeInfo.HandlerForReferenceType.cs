using CK.CodeGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CK.Setup.Json
{
    public partial class JsonTypeInfo
    {
        internal class HandlerForReferenceType : JsonCodeGenHandler
        {
            public override JsonTypeInfo TypeInfo { get; }
            public override bool IsNullable => true;
            public override string GenCSharpName => TypeInfo.GenCSharpName;

            readonly HandlerForNonNullableReferenceType _nonNullable;

            public HandlerForReferenceType( JsonTypeInfo info )
            {
                TypeInfo = info;
                _nonNullable = new HandlerForNonNullableReferenceType( this );
            }

            public override void GenerateWrite( ICodeWriter write, string variableName, bool? withType = null )
            {
                if( TypeInfo == ObjectType || (withType == null && !TypeInfo.IsFinal) )
                {
                    write.Append( "PocoDirectory_CK.WriteObject( w, " ).Append( variableName ).Append( ", options, false );" ).NewLine();
                }
                else
                {
                    DoGenerateWrite( write, variableName, handleNull: true, withType ?? false );
                }
            }

            public override void GenerateRead( ICodeWriter read, string variableName, bool assignOnly )
            {
                if( !TypeInfo.IsFinal )
                {
                    read.Append( variableName ).Append( " = (" ).Append( GenCSharpName )
                                               .Append( ")PocoDirectory_CK.ReadObject( ref r, options, false );" ).NewLine();
                }
                else
                {
                    this.DoGenerateRead( read, variableName, assignOnly );
                }
            }

            public override JsonCodeGenHandler ToNullHandler() => this;

            public override JsonCodeGenHandler ToNonNullHandler() => _nonNullable;
        }
    }
}
