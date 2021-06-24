using CK.CodeGen;
using CK.Core;
using CK.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CK.Setup.Json
{

    public partial class JsonSerializationCodeGen
    {

        /// <summary>
        /// Finalizes the code generation.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>True on success, false on error.</returns>
        public bool FinalizeCodeGeneration( IActivityMonitor monitor )
        {
            if( _finalizedCall.HasValue ) return _finalizedCall.Value;

            using( monitor.OpenInfo( $"Generating Json serialization with {_map.Count} mappings to {_typeInfos.Count} types." ) )
            {
                int missingCount = 0;
                foreach( var t in _typeInfos )
                {
                    if( t.CodeReader == null || t.CodeWriter == null )
                    {
                        ++missingCount;
                        using( _monitor.OpenTrace( $"Missing CodeReader/Writer for '{t.JsonName}'. Raising TypeInfoConfigurationRequired." ) )
                        {
                            try
                            {
                                TypeInfoConfigurationRequired?.Invoke( this, new TypeInfoConfigurationRequiredEventArg( _monitor, this, t ) );
                            }
                            catch( Exception ex )
                            {
                                _monitor.Error( $"While raising TypeInfoConfigurationRequired for '{t.JsonName}'.", ex );
                                _finalizedCall = false;
                                return false;
                            }
                        }
                    }
                }
                if( missingCount > 0 )
                {
                    // Let the TypeInfo be configured in any order (the event for Z may have configured A and Z together).
                    var missing = _typeInfos.Where( i => i.CodeWriter == null || i.CodeReader == null ).ToList();
                    if( missing.Count > 0 )
                    {
                        _monitor.Error( $"Missing Json CodeReader/Writer functions for types '{missing.Select( m => m.JsonName ).Concatenate( "', '" )}'." );
                        _finalizedCall = false;
                        return false;
                    }
                }
                // Generates the code for "dynamic"/"untyped" object.

                // Writing must handle the object instance to write. Null reference/value type can be handled immediately (by writing "null").
                // When not null, we are dealing only with concrete types here: the object MUST be of an allowed concrete type, an abstraction
                // that wouldn't be one of the allowed concrete type must NOT be handled!
                // That's why we can use a direct pattern matching on the object's type for the write method (reference types are ordered from specializations
                // to generalization).
                GenerateDynamicWrite( _typeInfos );

                // Reading must handle the [TypeName,...] array: it needs a lookup from the "type name" to the handler to use: this is the goal of
                // the _typeReaders dictionary that we initialize here (no concurrency issue, no lock to generate: once built the dictionary will only
                // be read).
                GenerateDynamicRead();

                string message = "While raising JsonTypeFinalized.";
                try
                {
                    JsonTypeFinalized?.Invoke( this, new EventMonitoredArgs( monitor ) );
                    message = "While executing deferred actions to GenerateRead/Write code.";
                    foreach( var a in _finalReadWrite )
                    {
                        a( monitor );
                    }
                }
                catch( Exception ex )
                {
                    _monitor.Error( message, ex );
                    _finalizedCall = false;
                    return false;
                }
                monitor.CloseGroup( "Success." );
                _finalizedCall = true;
                return true;
            }
        }

        void GenerateDynamicRead()
        {
            _pocoDirectory.GeneratedByComment()
                          .Append( @"
            delegate object ReaderFunction( ref System.Text.Json.Utf8JsonReader r, PocoJsonSerializerOptions options );

            static readonly Dictionary<string, ReaderFunction> _typeReaders = new Dictionary<string, ReaderFunction>();

            static readonly object oFalse = false;
            static readonly object oTrue = true;

            internal static object ReadObject( ref System.Text.Json.Utf8JsonReader r, PocoJsonSerializerOptions options )
            {
                object o;
                switch( r.TokenType )
                {
                    case System.Text.Json.JsonTokenType.Null: o = null; break;
                    case System.Text.Json.JsonTokenType.Number: o = r.GetDouble(); break;
                    case System.Text.Json.JsonTokenType.String: o = r.GetString(); break;
                    case System.Text.Json.JsonTokenType.False: o = oFalse; break;
                    case System.Text.Json.JsonTokenType.True: o = oTrue; break;
                    default:
                    {
                        if( r.TokenType != System.Text.Json.JsonTokenType.StartArray ) throw new System.Text.Json.JsonException( ""Expected 2-cells array."" );
                        r.Read(); // [
                        var n = r.GetString();
                        r.Read();
                        if( !_typeReaders.TryGetValue( n, out var reader ) )
                        {
                            throw new System.Text.Json.JsonException( $""Unregistered type name '{n}'."" );
                        }
                        o = reader( ref r, options );
                        if( r.TokenType != System.Text.Json.JsonTokenType.EndArray ) throw new System.Text.Json.JsonException( ""Expected end of 2-cells array."" );
                        break;
                    }
                }
                r.Read(); 
                return o;
            }
" );

            // Configures the _typeReaders dictionary in the constructor.
            var ctor = _pocoDirectory.FindOrCreateFunction( "public PocoDirectory_CK()" )
                                     .GeneratedByComment();
            foreach( var t in _typeInfos )
            {
                if( t.IsUntypedType ) continue;
                ctor.OpenBlock()
                    .Append( "static object d( ref System.Text.Json.Utf8JsonReader r, PocoJsonSerializerOptions options ) {" )
                    .AppendCSharpName( t.Type ).Append( " o;" ).NewLine();
                t.GenerateRead( ctor, "o", assignOnly: true, isNullableVariable: false );
                ctor.NewLine().Append( "return o;" ).NewLine()
                    .Append( "};" ).NewLine();
                var tName = t.NonNullHandler.JsonName;
                ctor.Append( "_typeReaders.Add( " ).AppendSourceString( tName ).Append( ", d );" ).NewLine();
                if( tName != t.NullHandler.JsonName )
                {
                    ctor.Append( "_typeReaders.Add( " ).AppendSourceString( t.NullHandler.JsonName ).Append( ", d );" ).NewLine();
                }
                ctor.CloseBlock();
            }

            foreach( var t in _standardReaders )
            {
                var f = _pocoDirectory.Append( "static object ECMAScriptStandardRead_" ).Append( t.JsonName ).Append( "( ref System.Text.Json.Utf8JsonReader r, PocoJsonSerializerOptions options )" )
                                      .OpenBlock();
                t.GenerateReadFunctionBody( f );
                _pocoDirectory.CloseBlock();

                ctor.Append( "_typeReaders.Add( " ).AppendSourceString( t.JsonName ).Append( ", ECMAScriptStandardRead_" ).Append( t.JsonName ).Append( " );" ).NewLine();
                if( t.MapNullableName )
                {
                    ctor.Append( "_typeReaders.Add( " ).AppendSourceString( t.JsonName + '?' ).Append( ", ECMAScriptStandardRead_" ).Append( t.JsonName ).Append( " );" ).NewLine();
                }
            }
        }

        void GenerateDynamicWrite( List<JsonTypeInfo> types )
        {
            _pocoDirectory
                    .GeneratedByComment()
                .Append( @"
internal static void WriteObject( System.Text.Json.Utf8JsonWriter w, object o, PocoJsonSerializerOptions options )
{
    switch( o )
    {
        case null: w.WriteNullValue(); break;" ).NewLine()
        .CreatePart( out var mappings ).Append( @"
        default: throw new System.Text.Json.JsonException( $""Unregistered type '{o.GetType().AssemblyQualifiedName}'."" );
    }
}" );
            foreach( var t in types )
            {
                // Skips direct types.
                if( t.IsUntypedType ) continue;
                mappings.Append( "case " ).AppendCSharpName( t.Type, useValueTupleParentheses: false ).Append( " v: " );
                Debug.Assert( !t.NonNullHandler.IsTypeMapping, "Only concrete Types are JsonTypeInfo, mapped types are just... mappings." );
                t.NonNullHandler.GenerateWrite( mappings, "v", true );
                mappings.NewLine().Append( "break;" ).NewLine();
            }
        }
    }
}
