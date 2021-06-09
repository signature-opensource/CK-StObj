using CK.CodeGen;
using System;
using System.Diagnostics;

namespace CK.Setup.Json
{
    public partial class JsonTypeInfo
    {
        class Handler : IJsonCodeGenHandler
        {
            public JsonTypeInfo TypeInfo { get; }
            public bool IsNullable { get; }
            public Type Type { get; }
            public string Name { get; }
            public bool IsMappedType { get; }

            readonly Handler _otherHandler;

            public Handler( JsonTypeInfo info, Type t, bool isNullable, bool isAbstractType )
            {
                IsNullable = isNullable;
                IsMappedType = isAbstractType;
                TypeInfo = info;
                Type = t;
                Name = info.Name;
                if( isNullable )
                {
                    Type = GetNullableType( t );
                    Name += '?';
                }
                _otherHandler = new Handler( this );
            }

            Handler( Handler other )
            {
                _otherHandler = other;
                IsNullable = !other.IsNullable;
                IsMappedType = other.IsMappedType;
                TypeInfo = other.TypeInfo;
                Name = other.TypeInfo.Name;
                if( IsNullable )
                {
                    Type = GetNullableType( other.Type );
                    Name += '?';
                }
                else Type = other.Type;
            }

            static Type GetNullableType( Type t )
            {
                if( t.IsValueType )
                {
                    t = typeof( Nullable<> ).MakeGenericType( t );
                }
                return t;
            }

            public bool IsReady => TypeInfo.IsFinal.HasValue;

            public void GenerateWrite( ICodeWriter write, string variableName, bool? withType = null, bool skipNullable = false )
            {
                if( !TypeInfo.IsFinal.HasValue ) throw new InvalidOperationException( $"Json Type '{Name}' requires Json Type finalization before GenerateWrite can be called." );
                if( TypeInfo.DirectType == JsonDirectType.Untyped || !TypeInfo.IsFinal.Value )
                {
                    write.Append( "PocoDirectory_CK.WriteObject( w, " ).Append( variableName ).Append( ");" ).NewLine();
                    return;
                }
                bool isNullable = IsNullable && !skipNullable;
                string? writeTypeName = (withType.HasValue ? withType.Value : IsMappedType) ? Name : null;
                TypeInfo.GenerateWrite( write, variableName, isNullable, writeTypeName );
            }

            public void GenerateRead( ICodeWriter read, string variableName, bool assignOnly, bool skipIfNullBlock = false )
            {
                if( !TypeInfo.IsFinal.HasValue ) throw new InvalidOperationException( $"Json Type '{Name}' requires Json Type finalization before GenerateRead can be called." );
                if( TypeInfo.DirectType == JsonDirectType.Untyped || TypeInfo.IsFinal == false )
                {
                    read.Append( variableName ).Append( " = (" ).AppendCSharpName( Type ).Append( ")PocoDirectory_CK.ReadObject( ref r );" ).NewLine();
                    return;
                }
                bool isNullable = IsNullable && !skipIfNullBlock;

                TypeInfo.GenerateRead( read, variableName, assignOnly, isNullable );
            }


            public IJsonCodeGenHandler CreateAbstract( Type t )
            {
                return new Handler( TypeInfo, t, IsNullable, true );
            }

            public IJsonCodeGenHandler ToNullHandler() => IsNullable ? this : _otherHandler;

            public IJsonCodeGenHandler ToNonNullHandler() => IsNullable ? _otherHandler : this;
        }
    }
}
