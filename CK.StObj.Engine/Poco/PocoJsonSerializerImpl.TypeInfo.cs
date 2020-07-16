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
        public delegate void CodeWriter( ICodeWriter write, string variableName, string pocoDirectoryAccessor );
        public delegate void CodeReader( ICodeWriter read, string variableName, bool assignOnly, bool isNullable, string pocoDirectoryAccessor );

        public enum DirectType
        {
            None,
            Untyped,
            String,
            Int,
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
            /// Gets the name with '!' or '?' suffix dependent on <see cref="IsNullable"/>.
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
            /// <param name="pocoDirectoryAccessor">The accessor to the PocoDirectory_CK class from the generated code.</param>
            /// <param name="withType">Tru or false to override <see cref="IsAbstractType"/>.</param>
            void GenerateWrite( ICodeWriter write, string variableName, string pocoDirectoryAccessor, bool? withType = null );

            /// <summary>
            /// Generates the code required to read a value into a <paramref name="variableName"/>.
            /// </summary>
            /// <param name="read">The code reader.</param>
            /// <param name="variableName">The variable name.</param>
            /// <param name="assignOnly">True to force the assignment of the variable, not trying to reuse it (typically because it is not initialized).</param>
            /// <param name="pocoDirectoryAccessor">The accessor to the PocoDirectory_CK class from the generated code.</param>
            void GenerateRead( ICodeWriter read, string variableName, bool assignOnly, string pocoDirectoryAccessor );

            /// <summary>
            /// Creates a handler for type that is mapped to this one.
            /// Its <see cref="IsAbstractType"/> is true.
            /// </summary>
            /// <param name="t">The mapped type.</param>
            /// <returns>An handler for the type.</returns>
            IHandler CreateAbstract( Type t );
        }


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

                public Handler( TypeInfo info, Type t, bool isNullable, bool isAbstractType )
                {
                    IsNullable = isNullable;
                    IsAbstractType = isAbstractType;
                    Info = info;
                    Type = t;
                    Name = info.Name;
                    if( isNullable )
                    {
                        if( t.IsValueType ) Name += '?';
                    }
                    else
                    {
                        if( !t.IsValueType ) Name += '!';
                    }
                }

                public void GenerateWrite( ICodeWriter write, string variableName, string pocoDirectoryAccessor, bool? withType = null )
                {
                    if( IsNullable )
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
                                bool writeType = Info.DirectType != DirectType.Untyped && (withType.HasValue ? withType.Value : IsAbstractType);
                                if( writeType )
                                {
                                    write.Append( "w.WriteStartArray(); w.WriteStringValue( " ).AppendSourceString( Info.Name ).Append( ");" ).NewLine();
                                }
                                Debug.Assert( Info._writer != null );
                                Info._writer( write, variableName, pocoDirectoryAccessor );
                                if( writeType )
                                {
                                    write.Append( "w.WriteEndArray();" ).NewLine();
                                }
                                break;
                            }
                    }
                    if( IsNullable ) write.CloseBlock();
                }

                public void GenerateRead( ICodeWriter read, string variableName, bool assignOnly, string pocoDirectoryAccessor )
                {
                    if( IsNullable )
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
                                Info._reader( read, variableName, assignOnly, IsNullable, pocoDirectoryAccessor );
                                break;
                            }
                    }
                    if( IsNullable ) read.CloseBlock();
                }

                public IHandler CreateAbstract( Type t )
                {
                    return new Handler( Info, t, IsNullable, true ); 
                }
            }


            public readonly ref struct RegKey
            {
                public readonly Type Type;
                public readonly string NumberName;

                public RegKey( Type t, string n )
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
            public IHandler NotNullHandler { get; }

            /// <summary>
            /// Gets the type name. Uses the ExternalNameAttribute, the type full name
            /// or a generated name for generic List, Set and Dictionary. 
            /// </summary>
            public string Name { get; }

            /// <summary>
            /// Gets the previous names (if any).
            /// </summary>
            public IReadOnlyList<string> PreviousNames { get; }

            /// <summary>
            /// Gets the <see cref="DirectType"/>.
            /// </summary>
            public DirectType DirectType { get; }

            public TypeInfo( in RegKey r, string name, IReadOnlyList<string>? previousNames = null, DirectType d = DirectType.None, bool isAbstractType = false )
            {
                Type = r.Type;
                Name = name;
                PreviousNames = previousNames ?? Array.Empty<string>();
                NumberName = r.NumberName;
                DirectType = d;
                if( Type.IsValueType )
                {
                    var tNull = typeof( Nullable<> ).MakeGenericType( Type );
                    NullHandler = new Handler( this, tNull, true, isAbstractType );
                    NotNullHandler = new Handler( this, Type, false, isAbstractType );
                }
                else
                {
                    NullHandler = new Handler( this, Type, true, isAbstractType );
                    NotNullHandler = new Handler( this, Type, false, isAbstractType );
                }
            }

            TypeInfo()
            {
                Type = typeof( object );
                Name = String.Empty;
                PreviousNames = Array.Empty<string>();
                NumberName = String.Empty;
                DirectType = DirectType.Untyped;
                _writer = ( ICodeWriter write, string variableName, string pocoDirectoryAccessor ) =>
                {
                    write.Append( pocoDirectoryAccessor ).Append( ".WriteObject( w, " ).Append( variableName ).Append( ");" );
                };
                _reader = ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable, string pocoDirectoryAccessor ) =>
                {
                    read.Append( variableName ).Append( " = " ).Append( pocoDirectoryAccessor ).Append( ".ReadObject( ref r );" );
                };
                NullHandler = new Handler( this, Type, true, true );
                NotNullHandler = new Handler( this, Type, false, true );
            }

            public TypeInfo Configure( CodeWriter w, CodeReader r )
            {
                _writer = w;
                _reader = r;
                return this;
            }
        }

    }
}
