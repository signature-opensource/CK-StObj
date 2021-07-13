using CK.CodeGen;
using System;

namespace CK.Setup.Json
{
    public static class IJsonCodeHandlerExtensions
    {
        /// <summary>
        /// Calls <see cref="JsonTypeInfo.CodeWriter"/> inside code that handles type discriminator and nullable.
        /// </summary>
        /// <param name="write">The code target.</param>
        /// <param name="variableName">The variable name.</param>
        /// <param name="handleNull">
        /// When true, handles the null value of the <paramref name="variableName"/>, either:
        /// <list type="bullet">
        /// <item>When <see cref="IJsonCodeGenHandler.IsNullable"/> is true: by writing null if the variable is null.</item>
        /// <item>When <see cref="IJsonCodeGenHandler.IsNullable"/> is false: by throwing an InvalidOperationException if the variable is null.</item>
        /// </list>.
        /// When false, no check is emitted, the variable is NOT null by design (its potential nullability has already been handled).
        /// </param>
        /// <param name="writeTypeName">True if type discriminator must be written.</param>
        public static void DoGenerateWrite( this IJsonCodeGenHandler @this, ICodeWriter write, string variableName, bool handleNull, bool writeTypeName )
        {
            if( @this == null ) throw new ArgumentNullException( nameof( @this ) );
            if( @this.TypeInfo.CodeWriter == null ) throw new InvalidOperationException( "CodeWriter has not been set." );
            bool variableCanBeNull = false;
            if( handleNull )
            {
                if( @this.IsNullable )
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
            if( variableCanBeNull && @this.Type.Type.IsValueType )
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

        /// <summary>
        /// Calls <see cref="CodeReader"/> inside code that handles reading null if <paramref name="isNullableVariable"/> is true
        /// (otherwise simply calls CodeReader).
        /// </summary>
        /// <param name="read">The code target.</param>
        /// <param name="variableName">The variable name.</param>
        /// <param name="assignOnly">True is the variable must be only assigned: no in-place read is possible.</param>
        public static void DoGenerateRead( this IJsonCodeGenHandler @this, ICodeWriter read, string variableName, bool assignOnly )
        {
            if( @this == null ) throw new ArgumentNullException( nameof( @this ) );
            if( @this.TypeInfo.CodeReader == null ) throw new InvalidOperationException( "CodeReader has not been set." );

            /// handleNull is only false for value types.
            bool handleNull = @this is not JsonTypeInfo.HandlerForValueType;
            // Instead of generating a throw here, rely on the reader error that will happen.
            if( handleNull && @this.IsNullable )
            {
                read.Append( "if( r.TokenType == System.Text.Json.JsonTokenType.Null )" )
                    .OpenBlock()
                    .Append( variableName ).Append( " = null;" ).NewLine()
                    .Append( "r.Read();" )
                    .CloseBlock()
                    .Append( "else" )
                    .OpenBlock();
            }
            @this.TypeInfo.CodeReader( read, variableName, assignOnly, @this.IsNullable );
            if( handleNull && @this.IsNullable ) read.CloseBlock();
        }
    }
}
