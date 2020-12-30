using CK.CodeGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

#nullable enable

namespace CK.Setup
{
    public partial class PocoJsonSerializerImpl
    {
        /// <summary>
        /// The code writer delegate is in charge of generating the write code.
        /// </summary>
        /// <param name="write">The code writer to uses.</param>
        /// <param name="variableName">The variable name to write.</param>
        public delegate void CodeWriter( ICodeWriter write, string variableName );

        /// <summary>
        /// The code reader delegate is in charge of generating the read code.
        /// </summary>
        /// <param name="read">The code writer to use.</param>
        /// <param name="variableName">The variable name.</param>
        /// <param name="assignOnly">True is the variable must be only assigned: no in-place read is possible.</param>
        /// <param name="isNullable">True if the variable can be null, false if it cannot be null.</param>
        public delegate void CodeReader( ICodeWriter read, string variableName, bool assignOnly, bool isNullable );

        /// <summary>
        /// Defines basic, direct types that are directly handled.
        /// </summary>
        public enum DirectType
        {
            /// <summary>
            /// Regular type.
            /// </summary>
            None,

            /// <summary>
            /// Untyped is handled by Read/WriteObject.
            /// </summary>
            Untyped,

            /// <summary>
            /// A raw string.
            /// </summary>
            String,

            /// <summary>
            /// A number is, by default, an integer.
            /// </summary>
            Int,

            /// <summary>
            /// Raw boolean type.
            /// </summary>
            Bool
        }

        /// <summary>
        /// Type handler with support for nullable types (value types as well as reference types)
        /// and abstract mapping.
        /// </summary>
        public interface IHandler
        {
            /// <summary>
            /// Gets the type handled.
            /// It can differ from the <see cref="TypeInfo.Type"/> if it's
            /// a value type and <see cref="IsNullable"/> is true (type is <see cref="Nullable{T}"/>)
            /// or if this <see cref="IsAbstractType"/> is true.
            /// </summary>
            Type Type { get; }

            /// <summary>
            /// Gets the name with '?' suffix if <see cref="IsNullable"/> is true.
            /// </summary>
            string Name { get; }

            /// <summary>
            /// Gets the <see cref="TypeInfo"/>.
            /// </summary>
            TypeInfo Info { get; }

            /// <summary>
            /// Gets whether this <see cref="Type"/> is not the same as the actual <see cref="TypeInfo.Type"/>
            /// and that it is not unambiguously mapped to it: the actual type name must be written in order
            /// to resolve it.
            /// </summary>
            bool IsAbstractType { get; }

            /// <summary>
            /// Gets whether this <see cref="Type"/> must be considered as a nullable one.
            /// </summary>
            bool IsNullable { get; }

            /// <summary>
            /// Generates the code required to write a value stored in <paramref name="variableName"/>.
            /// </summary>
            /// <param name="write">The code writer.</param>
            /// <param name="variableName">The variable name.</param>
            /// <param name="withType">True or false to override <see cref="IsAbstractType"/>.</param>
            /// <param name="skipIfNullBlock">
            /// True to skip the "if( variableName == null )" block whenever <see cref="IsNullable"/> is true.
            /// This <see cref="Type"/> and <see cref="Name"/> are kept as-is.
            /// </param>
            void GenerateWrite( ICodeWriter write, string variableName, bool? withType = null, bool skipIfNullBlock = false );

            /// <summary>
            /// Generates the code required to read a value into a <paramref name="variableName"/>.
            /// </summary>
            /// <param name="read">The code reader.</param>
            /// <param name="variableName">The variable name.</param>
            /// <param name="assignOnly">True to force the assignment of the variable, not trying to reuse it (typically because it is not initialized).</param>
            /// <param name="skipIfNullBlock">
            /// True to skip the "if( variableName == null )" block whenever <see cref="IsNullable"/> is true.
            /// </param>
            void GenerateRead( ICodeWriter read, string variableName, bool assignOnly, bool skipIfNullBlock = false );

            /// <summary>
            /// Creates a handler for type that is mapped to this one.
            /// Its <see cref="IsAbstractType"/> is true.
            /// </summary>
            /// <param name="t">The mapped type.</param>
            /// <returns>An handler for the type.</returns>
            IHandler CreateAbstract( Type t );

            /// <summary>
            /// Returns either this handler or its nullable companion.
            /// </summary>
            /// <returns>The nullable handler for the type as a nullable one.</returns>
            IHandler ToNullHandler();

            /// <summary>
            /// Returns either this handler or its non nullable companion.
            /// </summary>
            /// <returns>The non nullable handler for the type.</returns>
            IHandler ToNonNullHandler();
        }

        /// <summary>
        /// Centralized type representation that holds the null and non-null handlers and
        /// carries the <see cref="CodeReader"/> and <see cref="CodeWriter"/>.
        /// </summary>
        public class TypeInfo
        {
            /// <summary>
            /// Singleton for "object".
            /// Its handlers are <see cref="IHandler.IsAbstractType"/>.
            /// </summary>
            public static readonly TypeInfo Untyped = new TypeInfo();

            CodeWriter? _writer;
            CodeReader? _reader;

            class Handler : IHandler
            {
                public TypeInfo Info { get; }
                public bool IsNullable { get; }
                public Type Type { get; }
                public string Name { get; }
                public bool IsAbstractType { get; }

                readonly Handler _otherHandler;

                public Handler( TypeInfo info, Type t, bool isNullable, bool isAbstractType )
                {
                    IsNullable = isNullable;
                    IsAbstractType = isAbstractType;
                    Info = info;
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
                    IsAbstractType = other.IsAbstractType;
                    Info = other.Info;
                    Name = other.Info.Name;
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
                    if( Info.DirectType == DirectType.Untyped )
                    {
                        write.Append( "PocoDirectory_CK.WriteObject( w, " ).Append( variableName ).Append( ");" ).NewLine();
                        return;
                    }
                    bool isNullable = IsNullable && !skipNullable;
                    if( isNullable )
                    {
                        write.Append( "if( " ).Append( variableName ).Append( " == null ) w.WriteNullValue();" ).NewLine()
                                .Append( "else " )
                                .OpenBlock();
                    }
                    switch( Info.DirectType )
                    {
                        case DirectType.Int: write.Append( "w.WriteNumberValue( " ).Append( variableName ).Append( " );" ); break;
                        case DirectType.String: write.Append( "w.WriteStringValue( " ).Append( variableName ).Append( " );" ); break;
                        case DirectType.Bool: write.Append( "w.WriteBooleanValue( " ).Append( variableName ).Append( " );" ); break;
                        default:
                            {
                                bool writeType = (withType.HasValue ? withType.Value : IsAbstractType);
                                if( writeType )
                                {
                                    write.Append( "w.WriteStartArray(); w.WriteStringValue( " ).AppendSourceString( Name ).Append( ");" ).NewLine();
                                }
                                Debug.Assert( Info._writer != null );
                                bool hasBlock = false;
                                if( isNullable && Type.IsValueType )
                                {
                                    if( Info.ByRefWriter )
                                    {
                                        hasBlock = true;
                                        write.OpenBlock()
                                             .Append( "var notNull = " ).Append( variableName ).Append( ".Value;" ).NewLine();
                                        variableName = "notNull";
                                    }
                                    else variableName += ".Value";
                                }
                                Info._writer( write, variableName );
                                if( hasBlock )
                                {
                                    write.CloseBlock();
                                }
                                if( writeType )
                                {
                                    write.Append( "w.WriteEndArray();" ).NewLine();
                                }
                                break;
                            }
                    }
                    if( isNullable ) write.CloseBlock();
                }

                public void GenerateRead( ICodeWriter read, string variableName, bool assignOnly, bool skipIfNullBlock = false )
                {
                    if( Info.DirectType == DirectType.Untyped )
                    {
                        read.Append( variableName ).Append( " = (" ).AppendCSharpName( Type ).Append( ")PocoDirectory_CK.ReadObject( ref r );" ).NewLine();
                        return;
                    }
                    bool isNullable = IsNullable && !skipIfNullBlock;
                    if( isNullable )
                    {
                        read.Append( "if( r.TokenType == System.Text.Json.JsonTokenType.Null )" )
                            .OpenBlock()
                            .Append( variableName ).Append( " = null;" ).NewLine()
                            .Append( "r.Read();" )
                            .CloseBlock()
                            .Append( "else" )
                            .OpenBlock();
                    }
                    switch( Info.DirectType )
                    {
                        case DirectType.Int: read.Append( variableName ).Append( " = r.GetInt32(); r.Read();" ); break;
                        case DirectType.String: read.Append( variableName ).Append( " = r.GetString(); r.Read();" ); break;
                        case DirectType.Bool: read.Append( variableName ).Append( " = r.GetBoolean(); r.Read();" ); break;
                        default:
                            {
                                Debug.Assert( Info._reader != null );
                                Info._reader( read, variableName, assignOnly, isNullable );
                                break;
                            }
                    }
                    if( isNullable ) read.CloseBlock();
                }

                public IHandler CreateAbstract( Type t )
                {
                    return new Handler( Info, t, IsNullable, true ); 
                }

                public IHandler ToNullHandler() => IsNullable ? this : _otherHandler;

                public IHandler ToNonNullHandler() => IsNullable ? _otherHandler : this;
            }

            /// <summary>
            /// Pre registration key: the <see cref="Type"/> is bound to a <see cref="NumberName"/>.
            /// </summary>
            public readonly ref struct RegKey
            {
                /// <summary>
                /// The pre-registered type.
                /// </summary>
                public readonly Type Type;

                /// <summary>
                /// The associated number name.
                /// </summary>
                public readonly string NumberName;

                internal RegKey( Type t, string n )
                {
                    if( t.IsValueType && Nullable.GetUnderlyingType( t ) != null ) throw new ArgumentException( "Nullable value type must not be registered." );
                    Type = t;
                    NumberName = n;
                }
            }

            /// <summary>
            /// Gets the primary type.
            /// For value type, this is the not nullable type.
            /// </summary>
            public Type Type { get; }

            /// <summary>
            /// A unique incremented integer as a string.
            /// </summary>
            public string NumberName { get; }

            /// <summary>
            /// Nullable type handler.
            /// </summary>
            public IHandler NullHandler { get; }

            /// <summary>
            /// Not nullable type handler.
            /// </summary>
            public IHandler NonNullHandler { get; }

            /// <summary>
            /// Gets the type name. Uses the ExternalNameAttribute, the type full name
            /// or a generated name for generic List, Set and Dictionary. 
            /// </summary>
            public string Name { get; }

            /// <summary>
            /// Gets whether the writer uses a 'ref' parameter.
            /// This should be used for large struct.
            /// </summary>
            public bool ByRefWriter { get; private set; }

            /// <summary>
            /// Gets the previous names (if any).
            /// </summary>
            public IReadOnlyList<string> PreviousNames { get; }

            /// <summary>
            /// Gets the <see cref="DirectType"/>.
            /// </summary>
            public DirectType DirectType { get; }

            /// <summary>
            /// INitializes a new <see cref="TypeInfo"/>.
            /// </summary>
            /// <param name="r">The pre-registration key.</param>
            /// <param name="name">The type name.</param>
            /// <param name="previousNames">Optional previous names.</param>
            /// <param name="d">Optional <see cref="DirectType"/>.</param>
            /// <param name="isAbstractType">True for an abstract type (type mapping).</param>
            public TypeInfo( in RegKey r, string name, IReadOnlyList<string>? previousNames = null, DirectType d = DirectType.None, bool isAbstractType = false )
            {
                Type = r.Type;
                NumberName = r.NumberName;
                Name = name;
                PreviousNames = previousNames ?? Array.Empty<string>();
                DirectType = d;
                NonNullHandler = new Handler( this, Type, false, isAbstractType );
                NullHandler = NonNullHandler.ToNullHandler();
            }

            TypeInfo()
            {
                // _writer and _reader are unused and let to null.
                Type = typeof( object );
                Name = String.Empty;
                PreviousNames = Array.Empty<string>();
                NumberName = String.Empty;
                DirectType = DirectType.Untyped;
                NonNullHandler = new Handler( this, Type, false, true );
                NullHandler = NonNullHandler.ToNullHandler();
            }

            /// <summary>
            /// Sets the writer and reader delegates.
            /// </summary>
            /// <param name="w">The writer.</param>
            /// <param name="r">The reader.</param>
            /// <returns>This type info.</returns>
            public TypeInfo Configure( CodeWriter w, CodeReader r )
            {
                _writer = w;
                _reader = r;
                return this;
            }

            /// <summary>
            /// Sets whether the <see cref="CodeWriter"/> uses a "ref" for the variable.
            /// </summary>
            /// <returns>This type info.</returns>
            public TypeInfo SetByRefWriter()
            {
                ByRefWriter = true;
                return this;
            }
        }

    }
}
