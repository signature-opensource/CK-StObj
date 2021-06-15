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
            public bool IsTypeMapping { get; }

            readonly Handler _otherHandler;

            public Handler( JsonTypeInfo info, Type t, bool isNullable, bool isTypeMapping )
            {
                IsNullable = isNullable;
                IsTypeMapping = isTypeMapping;
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
                IsTypeMapping = other.IsTypeMapping;
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

            public void GenerateWrite( ICodeWriter write, string variableName, bool? withType = null, bool skipNullable = false )
            {
                if( !TypeInfo.IsFinal )
                {
                    write.Append( "PocoDirectory_CK.WriteObject( w, " ).Append( variableName ).Append( ", options );" ).NewLine();
                    return;
                }
                bool isNullable = IsNullable && !skipNullable;
                TypeInfo.GenerateWrite( write, variableName, isNullable, withType.HasValue ? withType.Value : IsTypeMapping );
            }

            public void GenerateRead( ICodeWriter read, string variableName, bool assignOnly, bool skipIfNullBlock = false )
            {
                if( !TypeInfo.IsFinal )
                {
                    read.Append( variableName ).Append( " = (" ).AppendCSharpName( Type ).Append( ")PocoDirectory_CK.ReadObject( ref r, options );" ).NewLine();
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
