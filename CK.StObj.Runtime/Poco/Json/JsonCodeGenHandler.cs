using CK.CodeGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CK.Setup.Json
{
    /// <summary>
    /// Type handler with support for nullable types (value types as well as reference types)
    /// and abstract mapping.
    /// </summary>
    public abstract class JsonCodeGenHandler
    {
        /// <summary>
        /// Gets the <see cref="JsonTypeInfo"/>.
        /// </summary>
        public abstract JsonTypeInfo TypeInfo { get; }

        /// <summary>
        /// Gets the type handled.
        /// It can differ from the <see cref="JsonTypeInfo.Type"/> if nullability differs from
        /// the "null normality" or this <see cref="TypeMapping"/> is not null.
        /// </summary>
        public virtual NullableTypeTree Type => TypeInfo.Type;

        /// <summary>
        /// Gets the <see cref="JsonTypeInfo.NumberName"/> with 'N' suffix if <see cref="IsNullable"/> is true.
        /// </summary>
        public string NumberName => IsNullable ? TypeInfo.NumberName + "N" : TypeInfo.NumberName;

        /// <summary>
        /// Gets the type name used in generated code.
        /// For value types, when <see cref="IsNullable"/> is true, the suffix '?' is appended.
        /// For reference type, it is always the oblivious <see cref="JsonTypeInfo.GenCSharpName"/>.
        /// For type mapping (that are always reference types), this is the oblivious mapped type name, not the target mapping's one.
        /// </summary>
        public virtual string GenCSharpName => TypeInfo.GenCSharpName;

        /// <summary>
        /// Gets the JSON (safe mode) name with '?' suffix if <see cref="IsNullable"/> is true.
        /// <para>
        /// This uses the ExternalNameAttribute, the type full name
        /// or a generated name for arrays, generic List, Set and Dictionary.
        /// </para>
        /// </summary>
        public virtual string JsonName => IsNullable ? TypeInfo.NonNullableJsonName + '?' : TypeInfo.NonNullableJsonName;

        /// <summary>
        /// Gets the previous names if any (there is no previous names for ECMA standard mode since only basic types can have a standard name).
        /// These names have a '?' suffix if <see cref="IsNullable"/> is true.
        /// </summary>
        public virtual IEnumerable<string> PreviousJsonNames => IsNullable ? TypeInfo.NonNullablePreviousJsonNames.Select( n => n + '?' ) : TypeInfo.NonNullablePreviousJsonNames;

        /// <summary>
        /// Gets the JSON name used when "ECMAScript standard" is used.
        /// For non collection types, an <see cref="ECMAScriptStandardReader"/> should be registered for this name
        /// so that 'object' can be read.
        /// <para>
        /// The nullable name is canonical if the non nullable name is canonical since there cannot be a
        /// nullable and a non nullable of the same type in an union type (the non nullable one has been removed).
        /// </para>
        /// </summary>
        public virtual ECMAScriptStandardJsonName ECMAScriptStandardJsonName => IsNullable
                                                                                ? new( TypeInfo.NonNullableECMAScriptStandardJsonName.Name + '?', TypeInfo.NonNullableECMAScriptStandardJsonName.IsCanonical )
                                                                                : TypeInfo.NonNullableECMAScriptStandardJsonName;

        /// <summary>
        /// Gets whether this <see cref="JsonName"/> differs from this <see cref="Json.ECMAScriptStandardJsonName"/>.
        /// </summary>
        public bool HasECMAScriptStandardJsonName => TypeInfo.NonNullableECMAScriptStandardJsonName.Name != TypeInfo.NonNullableJsonName;

        /// <summary>
        /// Gets a handler that unambiguously handles this <see cref="Type"/>: this Type is not the same as the
        /// actual <see cref="JsonTypeInfo.Type"/>.
        /// </summary>
        public virtual JsonCodeGenHandler? TypeMapping => null;

        /// <summary>
        /// Gets whether this <see cref="Type"/> must be considered as a nullable one.
        /// </summary>
        public virtual bool IsNullable => false;

        /// <summary>
        /// Generates the code required to write a value stored in <paramref name="variableName"/>.
        /// </summary>
        /// <param name="write">The code writer.</param>
        /// <param name="variableName">The variable name.</param>
        /// <param name="withType">
        /// True or false ignores <see cref="JsonTypeInfo.IsFinal"/>. By default, when IsFinal is false (applies to
        /// reference types only) a call to the generic Write( object ) is generated.
        /// </param>
        public abstract void GenerateWrite( ICodeWriter write, string variableName, bool? withType = null );

        /// <summary>
        /// Generates the code required to read a value into a <paramref name="variableName"/>.
        /// This calls <see cref="JsonCodeGenHandler.GenerateRead(ICodeWriter, string, bool)"/> (with <see cref="IsNullable"/>) or, if
        /// <see cref="JsonTypeInfo.IsFinal"/> is false (applies to reference types only) a call
        /// to the generic ReadObject method is generated.
        /// </summary>
        /// <param name="read">The code reader.</param>
        /// <param name="variableName">The variable name.</param>
        /// <param name="assignOnly">
        /// True to force the assignment of the variable, not trying to reuse it (typically because it is known to be uninitialized).
        /// This is used for collections (that can be cleared) and Poco (that may be already instantiated).
        /// </param>
        public abstract void GenerateRead( ICodeWriter read, string variableName, bool assignOnly );

        /// <summary>
        /// Returns either this handler or its nullable companion.
        /// </summary>
        /// <returns>The nullable handler for the type as a nullable one.</returns>
        public abstract JsonCodeGenHandler ToNullHandler();

        /// <summary>
        /// Returns either this handler or its non nullable companion.
        /// </summary>
        /// <returns>The non nullable handler for the type.</returns>
        public abstract JsonCodeGenHandler ToNonNullHandler();

        /// <summary>
        /// Calls <see cref="JsonTypeInfo.CodeWriter"/> inside code that handles type discriminator and nullable.
        /// </summary>
        /// <param name="write">The code target.</param>
        /// <param name="variableName">The variable name.</param>
        /// <param name="handleNull">
        /// When true, handles the null value of the <paramref name="variableName"/>, either:
        /// <list type="bullet">
        /// <item>When <see cref="JsonCodeGenHandler.IsNullable"/> is true: by writing null if the variable is null.</item>
        /// <item>When <see cref="JsonCodeGenHandler.IsNullable"/> is false: by throwing an InvalidOperationException if the variable is null.</item>
        /// </list>.
        /// When false, no check is emitted, the variable is NOT null by design (its potential nullability should have already been handled).
        /// </param>
        /// <param name="writeTypeName">True if type discriminator must be written.</param>
        public void DoGenerateWrite( ICodeWriter write, string variableName, bool handleNull, bool writeTypeName )
        {
            if( TypeInfo.CodeWriter == null ) throw new InvalidOperationException( "CodeWriter has not been set." );
            bool variableCanBeNull = false;
            if( handleNull )
            {
                if( IsNullable )
                {
                    write.Append( "if( " ).Append( variableName ).Append( " == null ) w.WriteNullValue();" ).NewLine()
                            .Append( "else " )
                            .OpenBlock();
                    variableCanBeNull = true;
                }
                else
                {
                    write.Append( "if( " ).Append( variableName ).Append( " == null ) throw new InvalidOperationException(\"A null value appear where it should not. Writing JSON is impossible.\");" ).NewLine();
                }
            }
            writeTypeName &= !TypeInfo.IsIntrinsic;
            if( writeTypeName )
            {
                write.Append( "w.WriteStartArray(); w.WriteStringValue( " );
                if( HasECMAScriptStandardJsonName )
                {
                    write.Append( "options?.Mode == PocoJsonSerializerMode.ECMAScriptStandard ? " ).AppendSourceString( ECMAScriptStandardJsonName.Name )
                            .Append( " : " );
                }
                write.AppendSourceString( JsonName ).Append( " );" ).NewLine();
            }
            bool hasBlock = false;
            if( variableCanBeNull && Type.Type.IsValueType )
            {
                if( TypeInfo.ByRefWriter )
                {
                    hasBlock = true;
                    write.OpenBlock()
                            .Append( "var notNull = " ).Append( variableName ).Append( ".Value;" ).NewLine();
                    variableName = "notNull";
                }
                else variableName += ".Value";
            }
            TypeInfo.CodeWriter( write, variableName );
            if( hasBlock )
            {
                write.CloseBlock();
            }
            if( writeTypeName )
            {
                write.Append( "w.WriteEndArray();" ).NewLine();
            }
            if( variableCanBeNull ) write.CloseBlock();
        }

        /// <summary>
        /// Calls <see cref="CodeReader"/> inside code that handles reading null if <paramref name="handleNull"/> is true
        /// (otherwise simply calls CodeReader).
        /// </summary>
        /// <param name="read">The code target.</param>
        /// <param name="variableName">The variable name.</param>
        /// <param name="assignOnly">True is the variable must be only assigned: no in-place read is possible.</param>
        /// <param name="handleNull">
        /// By default a code is generated that handles the null token and either sets the variable to null or throws a JsonException
        /// depending on <see cref="JsonCodeGenHandler.IsNullable"/>.
        /// This can be set to false if the null token has already been handled (before a switch case for example).
        /// <para>
        /// Note that when this is an handler for non nullable value type, this block is not generated by default since the exception
        /// will be "naturally" thrown by the CodeReader.
        /// </para>
        /// </param>
        public void DoGenerateRead( ICodeWriter read, string variableName, bool assignOnly, bool? handleNull = null )
        {
            if( TypeInfo.CodeReader == null ) throw new InvalidOperationException( "CodeReader has not been set." );

            bool hasNullBlock = false;
            if( handleNull ?? this is not JsonTypeInfo.HandlerForValueType )
            {
                read.Append( "if( r.TokenType == System.Text.Json.JsonTokenType.Null )" );
                if( IsNullable )
                {
                    read.OpenBlock()
                        .Append( variableName ).Append( " = null;" ).NewLine()
                        .Append( "r.Read();" )
                        .CloseBlock()
                        .Append( "else" )
                        .OpenBlock();
                    hasNullBlock = true;
                }
                else
                {
                    read.Append( " throw new System.Text.Json.JsonException( \"Unexpected null value.\");" ).NewLine();
                }
            }
            TypeInfo.CodeReader( read, variableName, assignOnly, IsNullable );
            if( hasNullBlock ) read.CloseBlock();
        }

    }
}
