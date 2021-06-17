using CK.CodeGen;
using CK.Core;
using CK.Setup;
using CK.Setup.Json;
using System;
using System.Reflection;

namespace CK.StObj.Engine.Tests.PocoJson
{
    /// <summary>
    /// This generates Json serialization for any type (not already Json aware) that has a
    /// static Parse( string ) method: ToString() is written and Parse( string ) is called
    /// to deserialize the instance.
    /// <para>
    /// This is just a sample, this should not be used in any code base (at least without
    /// major improvements).
    /// </para>
    /// <para>
    /// To automatically activate this generator, just reference the static <see cref="JsonStringParseSupport"/>
    /// type defined below.
    /// </para>
    /// </summary>
    public class JsonSerializerViaToStringAndParseGenerator : ICSCodeGenerator
    {
        CSCodeGenerationResult ICSCodeGenerator.Implement( IActivityMonitor monitor, ICSCodeGenerationContext codeGenContext ) => new CSCodeGenerationResult( nameof( WithJsonCodeGen ) );

        void WithJsonCodeGen( IActivityMonitor monitor, ICSCodeGenerationContext codeGenContext, JsonSerializationCodeGen json )
        {
            json.TypeInfoRequired += OnTypeInfoRequired;
        }

        void OnTypeInfoRequired( object? sender, TypeInfoRequiredEventArg e )
        {
            Type toSupport = e.RequiredType;
            // If the required type has not been already allowed by another
            // participant, we check if a static parse method exists that takes
            // a string and returns the type to support.
            MethodInfo? parseMethod;
            ParameterInfo[]? parseMethodParameters = null;
            if( !e.JsonCodeGen.IsAllowedType( toSupport )
                && (parseMethod = toSupport.GetMethod( "Parse", BindingFlags.Public | BindingFlags.Static )) != null
                && parseMethod.ReturnType == toSupport
                && (parseMethodParameters = parseMethod.GetParameters()).Length == 1
                && parseMethodParameters[0].ParameterType == typeof( string ) )
            {
                // The write is very simple: writes the ToString() as a Json string.
                // The read calls the static Parse on the reader.

                if( toSupport.GetExternalNames( e.Monitor, out var name, out var previousNames ) )
                {
                    e.JsonCodeGen.AllowTypeInfo( toSupport, name, StartTokenType.String, previousNames ).Configure(
                        ( write, variableName ) =>
                        {
                            write.Append( "w.WriteStringValue( " ).Append( variableName ).Append( ".ToString() );" ).NewLine();
                        },
                        ( read, variableName, assignOnly, isNullable ) =>
                        {
                            read.Append( variableName ).Append( " = " ).AppendCSharpName( toSupport ).Append( ".Parse( r.GetString() ); r.Read();" ).NewLine();
                        } );
                }
            }
        }
    }

    /// <summary>
    /// "Model type" that activates the support Json serialization for any type (not already Json aware) that has a
    /// static Parse method: ToString() is written and Parse is called to deserialize the instance.
    /// </summary>
    [ContextBoundDelegation( "CK.StObj.Engine.Tests.PocoJson.JsonSerializerViaToStringAndParseGenerator, CK.StObj.Engine.Tests" )]
    public static class JsonStringParseSupport { }


}
