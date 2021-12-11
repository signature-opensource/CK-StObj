using CK.CodeGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CK.Setup.Json
{
    public partial class JsonTypeInfo
    {
        internal class HandlerForNonNullableReferenceType : JsonCodeGenHandler
        {
            public override JsonTypeInfo TypeInfo => _nullHandler.TypeInfo;
            public override NullableTypeTree Type => TypeInfo.Type.ToAbnormalNull();

            readonly HandlerForReferenceType _nullHandler;

            public HandlerForNonNullableReferenceType( HandlerForReferenceType nullHandler )
            {
                _nullHandler = nullHandler;
            }

            public override void GenerateWrite( ICodeWriter write, string variableName, bool? withType = null )
            {
                if( TypeInfo == JsonTypeInfo.ObjectType || (withType == null && !TypeInfo.IsFinal) )
                {
                    write.Append( "PocoDirectory_CK.WriteObject( w, " ).Append( variableName ).Append( ", options, true );" ).NewLine();
                }
                else
                {
                    this.DoGenerateWrite( write, variableName, handleNull: true, withType ?? false );
                }
            }

            public override void GenerateRead( ICodeWriter read, string variableName, bool assignOnly )
            {
                if( !TypeInfo.IsFinal )
                {
                    read.Append( variableName ).Append( " = (" ).Append( GenCSharpName ).Append( ")PocoDirectory_CK.ReadObject( ref r, options, true );" ).NewLine();
                }
                else
                {
                    DoGenerateRead( read, variableName, assignOnly );
                }
            }

            public override JsonCodeGenHandler ToNullHandler() => _nullHandler;

            public override JsonCodeGenHandler ToNonNullHandler() => this;
        }
    }
}