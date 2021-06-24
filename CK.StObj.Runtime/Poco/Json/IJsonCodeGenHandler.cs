using CK.CodeGen;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Setup.Json
{
    /// <summary>
    /// Type handler with support for nullable types (value types as well as reference types)
    /// and abstract mapping.
    /// </summary>
    public interface IJsonCodeGenHandler
    {
        /// <summary>
        /// Gets the type handled.
        /// It can differ from the <see cref="JsonTypeInfo.Type"/> if it's
        /// a value type and <see cref="IsNullable"/> is true (type is <see cref="Nullable{T}"/>)
        /// or if this <see cref="IsTypeMapping"/> is true.
        /// </summary>
        Type Type { get; }

        /// <summary>
        /// Gets the <see cref="JsonTypeInfo.NumberName"/> with 'N' suffix if <see cref="IsNullable"/> is true.
        /// </summary>
        string NumberName => IsNullable ? TypeInfo.NumberName + "N" : TypeInfo.NumberName;

        /// <summary>
        /// Gets the JSON (safe mode) name with '?' suffix if <see cref="IsNullable"/> is true.
        /// <para>
        /// This uses the ExternalNameAttribute, the type full name
        /// or a generated name for arrays, generic List, Set and Dictionary.
        /// </para>
        /// </summary>
        string JsonName { get; }

        /// <summary>
        /// Gets the previous names if any (there is no previous names for ECMA standard mode since only basic types can have a standard name).
        /// These names have a '?' suffix if <see cref="IsNullable"/> is true.
        /// </summary>
        IEnumerable<string> PreviousJsonNames { get; }

        /// <summary>
        /// Gets the JSON name used when "ECMAScript standard" is used.
        /// For non collection types, an <see cref="ECMAScriptStandardReader"/> should be registered for this name
        /// so that 'object' can be read.
        /// </summary>
        ECMAScriptStandardJsonName ECMAScriptStandardJsonName { get; }

        /// <summary>
        /// Gets whether this <see cref="JsonName"/> differs from this <see cref="Json.ECMAScriptStandardJsonName"/>.
        /// </summary>
        public bool HasECMAScriptStandardJsonName => ECMAScriptStandardJsonName.Name != JsonName;

        /// <summary>
        /// Gets the <see cref="JsonTypeInfo"/>.
        /// </summary>
        JsonTypeInfo TypeInfo { get; }

        /// <summary>
        /// Gets whether this <see cref="Type"/> is not the same as the actual <see cref="TypeInfo.Type"/>
        /// and that it is not unambiguously mapped to it: the mapped type name must be written in order
        /// to resolve it.
        /// <para>
        /// Note that the type name is also written if <see cref="JsonTypeInfo.IsFinal"/> is false (since a base class
        /// may reference a specialization).
        /// </para>
        /// </summary>
        bool IsTypeMapping { get; }

        /// <summary>
        /// Gets whether this <see cref="Type"/> must be considered as a nullable one.
        /// </summary>
        bool IsNullable { get; }

        /// <summary>
        /// Generates the code required to write a value stored in <paramref name="variableName"/>.
        /// </summary>
        /// <param name="write">The code writer.</param>
        /// <param name="variableName">The variable name.</param>
        /// <param name="withType">
        /// True or false overrides <see cref="JsonTypeInfo.IsFinal"/>: it is the code write an object of this <see cref="Type"/>
        /// that is written, regardless of any <see cref="JsonTypeInfo.AllSpecializations"/>.
        /// </param>
        void GenerateWrite( ICodeWriter write, string variableName, bool? withType = null );

        /// <summary>
        /// Generates the code required to read a value into a <paramref name="variableName"/>.
        /// </summary>
        /// <param name="read">The code reader.</param>
        /// <param name="variableName">The variable name.</param>
        /// <param name="assignOnly">True to force the assignment of the variable, not trying to reuse it (typically because it is known to be uninitialized).</param>
        void GenerateRead( ICodeWriter read, string variableName, bool assignOnly );

        /// <summary>
        /// Returns either this handler or its nullable companion.
        /// </summary>
        /// <returns>The nullable handler for the type as a nullable one.</returns>
        IJsonCodeGenHandler ToNullHandler();

        /// <summary>
        /// Returns either this handler or its non nullable companion.
        /// </summary>
        /// <returns>The non nullable handler for the type.</returns>
        IJsonCodeGenHandler ToNonNullHandler();

    }

    public static class JsonCodeHandlerExtensions
    {
        /// <summary>
        /// Calls <see cref="JsonTypeInfo.CodeWriter"/> inside code that handles type discriminator and nullable.
        /// </summary>
        /// <param name="write">The code target.</param>
        /// <param name="variableName">The variable name.</param>
        /// <param name="variableCanBeNull">Whether null value of the <paramref name="variableName"/> must be handled.</param>
        /// <param name="writeTypeName">True if type discriminator must be written.</param>
        public static void DoGenerateWrite( this IJsonCodeGenHandler @this, ICodeWriter write, string variableName, bool variableCanBeNull, bool writeTypeName )
        {
            if( @this == null ) throw new ArgumentNullException( nameof( @this ) );
            if( @this.TypeInfo.CodeWriter == null ) throw new InvalidOperationException( "CodeWriter has not been set." );
            if( variableCanBeNull )
            {
                write.Append( "if( " ).Append( variableName ).Append( " == null ) w.WriteNullValue();" ).NewLine()
                        .Append( "else " )
                        .OpenBlock();
            }
            writeTypeName &= !@this.TypeInfo.IsIntrinsic;
            if( writeTypeName )
            {
                write.Append( "w.WriteStartArray(); w.WriteStringValue( " );
                if( @this.HasECMAScriptStandardJsonName )
                {
                    write.Append( "options?.Mode == PocoJsonSerializerMode.ECMAScriptStandard ? " ).AppendSourceString( @this.ECMAScriptStandardJsonName.Name )
                            .Append( " : " );
                }
                write.AppendSourceString( @this.JsonName ).Append( " );" ).NewLine();
            }
            bool hasBlock = false;
            if( variableCanBeNull && @this.Type.IsValueType )
            {
                if( @this.TypeInfo.ByRefWriter )
                {
                    hasBlock = true;
                    write.OpenBlock()
                            .Append( "var notNull = " ).Append( variableName ).Append( ".Value;" ).NewLine();
                    variableName = "notNull";
                }
                else variableName += ".Value";
            }
            @this.TypeInfo.CodeWriter( write, variableName );
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

    }
}
