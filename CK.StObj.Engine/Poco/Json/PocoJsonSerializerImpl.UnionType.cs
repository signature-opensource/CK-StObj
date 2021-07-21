#nullable enable

using CK.CodeGen;
using CK.Core;
using CK.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CK.Setup.Json
{
    public partial class PocoJsonSerializerImpl
    {
        PocoJsonPropertyInfo? HandleUnionType( IPocoPropertyInfo p,
                                               IActivityMonitor monitor,
                                               JsonSerializationCodeGen jsonCodeGen,
                                               ITypeScopePart write,
                                               ITypeScopePart read,
                                               ref bool isPocoECMAScriptStandardCompliant,
                                               JsonCodeGenHandler mainHandler )
        {
            // Analyses the UnionTypes and creates the handler for each of them.
            // - Forbids ambiguous mapping for ECMAScriptStandard: all numerics are mapped to "Number" or "BigInt" (and arrays or lists are arrays).
            // - The ECMAScriptStandard projected name must be unique (and is associated to its actual handler).
            var allHandlers = new List<JsonCodeGenHandler>();
            var checkDuplicatedStandardName = new Dictionary<string, List<JsonCodeGenHandler>>();
            // Gets all the handlers and build groups of ECMAStandardJsnoName handlers (only if the Poco is still standard compliant).
            foreach( var union in p.PropertyUnionTypes )
            {
                var h = jsonCodeGen.GetHandler( union );
                if( h == null ) return null;
                allHandlers.Add( h );
                if( isPocoECMAScriptStandardCompliant && h.HasECMAScriptStandardJsonName )
                {
                    var n = h.ECMAScriptStandardJsonName;
                    if( checkDuplicatedStandardName.TryGetValue( n.Name, out var exists ) )
                    {
                        exists.Add( h );
                    }
                    else
                    {
                        checkDuplicatedStandardName.Add( n.Name, new List<JsonCodeGenHandler>() { h } );
                    }
                }
            }
            allHandlers.Sort( ( h1, h2 ) => h1.TypeInfo.TypeSpecOrder.CompareTo( h2.TypeInfo.TypeSpecOrder ) );

            // Analyze the groups (only if the Poco is still standard compliant).
            List<JsonCodeGenHandler>? ecmaStandardReadhandlers = null;
            bool isECMAScriptStandardCompliant = isPocoECMAScriptStandardCompliant;
            if( isECMAScriptStandardCompliant )
            {
                foreach( var group in checkDuplicatedStandardName.Values )
                {
                    if( group.Count > 1 )
                    {
                        int idxCanocical = group.IndexOf( h => h.ECMAScriptStandardJsonName.IsCanonical );
                        if( idxCanocical == -1 )
                        {
                            monitor.Warn( $"{p} UnionType '{group.Select( h => h.GenCSharpName ).Concatenate( "' ,'" )}' types mapped to the same ECMAScript standard name: '{group[0].ECMAScriptStandardJsonName.Name}' and none of them is the 'Canonical' form. De/serializing this Poco in 'ECMAScriptstandard' will throw a NotSupportedException." );
                            isECMAScriptStandardCompliant = false;
                            break;
                        }
                        var winner = group[idxCanocical];
                        monitor.Trace( $"{p} UnionType '{group.Select( h => h.GenCSharpName ).Concatenate( "' ,'" )}' types will be read as {winner.GenCSharpName} in ECMAScript standard name." );
                        if( ecmaStandardReadhandlers == null ) ecmaStandardReadhandlers = allHandlers.Where( h => !h.HasECMAScriptStandardJsonName ).ToList();
                        ecmaStandardReadhandlers.Add( winner );
                    }
                    else
                    {
                        monitor.Debug( $"{p} UnionType unambiguous mapping in ECMAScript standard name from '{group[0].ECMAScriptStandardJsonName.Name}' to '{group[0].GenCSharpName}'." );
                        if( ecmaStandardReadhandlers == null ) ecmaStandardReadhandlers = allHandlers.Where( h => !h.HasECMAScriptStandardJsonName ).ToList();
                        ecmaStandardReadhandlers.Add( group[0] );
                    }
                }
                isPocoECMAScriptStandardCompliant &= isECMAScriptStandardCompliant;
            }
            // Invariant: handlers are by design associated to different "oblivious NRT" types: switch case can be done on them.
            // That means that the actual's object type is enough to identify the exact handler (in THE CONTEXT of this property type!).
            // And the property setter controls the assignation: the set of types is controlled.
            //
            // It is tempting to simply call the generic write function but this one uses the GenericWriteHandler that is the "oblivious NRT" type:
            // even if this union exposes a ISet<string>? (nullable of non-nullable), it will be a ISet<string?> (non-nullable - since the GenericWriteHandler
            // is by design a NonNullHandler - of nullable - that is the oblivious nullability for reference type) that will be serialized.
            // 
            // Actually the type name doesn't really matter, it's just a convention that a client must follow to receive or send data: here, we could
            // perfectly use the index of the type in the union types, that would be an identifier "local to this property" but this would do the job.
            //
            // What really matters is to identify the function that will read the data with the right null handling so that no nulls can be
            // injected where it should not AND to use the right function to write the data, the one that will not let unexpected nulls emitted.
            // Regarding this, using the GenericWriteHandler is definitely not right.
            //
            // That's why we generate a dedicated switch-case for writing here. If one of the handler is bound to the ObjectType (currently
            // that's true when jsonTypeInfo.IsFinal is false), we call the generic write object in the default: case.
            //
            Debug.Assert( allHandlers.Select( h => h.TypeInfo.GenCSharpName ).GroupBy( Util.FuncIdentity ).Count( g => g.Count() > 1 ) == 0 );

            var info = new PocoJsonPropertyInfo( p, allHandlers, isECMAScriptStandardCompliant ? ecmaStandardReadhandlers : null );
            _finalReadWrite.Add( () =>
            {
                int p;
                var fieldName = "_v" + info.PropertyInfo.Index;
                write.Append( "w.WritePropertyName( " ).AppendSourceString( info.PropertyInfo.PropertyName ).Append( " );" ).NewLine();
                if( info.IsJsonUnionType )
                {
                    write.GeneratedByComment()
                         .Append( @"switch( " ).Append( fieldName ).Append( " )" )
                         .OpenBlock()
                         .Append( "case null: " );
                    if( info.PropertyInfo.IsNullable )
                    {
                        write.Append( "w.WriteNullValue();" ).NewLine()
                             .Append( "break;" );
                    }
                    else
                    {
                        write.Append( @"throw new InvalidOperationException( ""A null value appear where it should not. Writing JSON is impossible."" );" );
                    }
                    write.NewLine();
                    bool hasDefaultObject = false;
                    foreach( var h in info.AllHandlers )
                    {
                        Debug.Assert( !h.IsNullable, "Union types are not nullable by design (null has been generalized)." );
                        if( h.TypeInfo.IsFinal )
                        {
                            write.Append( "case " ).Append( h.TypeInfo.MostAbstractMapping?.GenCSharpName ?? h.TypeInfo.GenCSharpName ).Append( " v: " ).NewLine();
                            h.DoGenerateWrite( write, "v", handleNull: false, writeTypeName: true );
                            write.NewLine().Append( "break;" ).NewLine();
                        }
                        else hasDefaultObject = true;
                    }
                    write.Append( @"default:" ).NewLine();
                    if( hasDefaultObject )
                    {
                        mainHandler.ToNonNullHandler().GenerateWrite( write, fieldName );
                        write.NewLine().Append( "break;" );
                    }
                    else
                    {
                        write.Append( @"throw new InvalidOperationException( $""Unexpected type {" ).Append( fieldName ).Append( @".GetType()} in union " ).Append( info.PropertyInfo.ToString()! ).Append( @"."" );" );
                    }
                    write.CloseBlock();
                }
                else
                {
                    info.AllHandlers[0].GenerateWrite( write, fieldName );
                }

                read.Append( "case " ).AppendSourceString( info.PropertyInfo.PropertyName ).Append( ": " )
                    .OpenBlock();

                if( info.IsJsonUnionType )
                {
                    read.Append( "if( r.TokenType == System.Text.Json.JsonTokenType.Null )" );
                    if( info.PropertyInfo.IsNullable )
                    {
                        read.OpenBlock()
                            .Append( fieldName ).Append( " = null;" ).NewLine()
                            .Append( "r.Read();" )
                            .CloseBlock()
                            .Append( "else" )
                            .OpenBlock();
                    }
                    else
                    {
                        read.Append( " throw new System.Text.Json.JsonException(\"" ).Append( info.PropertyInfo.ToString()! ).Append( " cannot be null.\");" ).NewLine();
                    }

                    if( info.IsJsonUnionType )
                    {
                        static void OpenSwitchOnNameBlock( ICodeWriter read )
                        {
                            read.Append( "if( r.TokenType != System.Text.Json.JsonTokenType.StartArray ) throw new System.Text.Json.JsonException( \"Expecting Json Type array.\" );" ).NewLine()
                            .Append( "r.Read();" ).NewLine()
                            .Append( "string name = r.GetString();" ).NewLine()
                            .Append( "r.Read();" ).NewLine()
                            .Append( "switch( name )" )
                            .OpenBlock();
                        }
                        bool hasIntrinsics = false;
                        var intrinsicPart = read.CreatePart();
                        bool hasDefaultObject = false;
                        foreach( var h in info.AllHandlers )
                        {
                            if( h.TypeInfo.IsFinal )
                            {
                                if( h.TypeInfo.IsIntrinsic )
                                {
                                    if( !hasIntrinsics )
                                    {
                                        hasIntrinsics = true;
                                        intrinsicPart.Append( "switch( r.TokenType )" )
                                                     .OpenBlock();
                                    }
                                    if( h.Type.Type == typeof( bool ) )
                                    {
                                        intrinsicPart.Append( "case System.Text.Json.JsonTokenType.True: " ).Append( fieldName ).Append( " = true; r.Read(); break;" ).NewLine()
                                                     .Append( "case System.Text.Json.JsonTokenType.False: " ).Append( fieldName ).Append( " = false; r.Read(); break;" ).NewLine();
                                    }
                                    else if( h.Type.Type == typeof( string ) )
                                    {
                                        intrinsicPart.Append( "case System.Text.Json.JsonTokenType.String: " ).Append( fieldName ).Append( " = r.GetString(); r.Read(); break;" ).NewLine();
                                    }
                                    else
                                    {
                                        Debug.Assert( h.Type.Type == typeof( double ) );
                                        intrinsicPart.Append( "case System.Text.Json.JsonTokenType.Number: " ).Append( fieldName ).Append( " = r.GetDouble(); r.Read(); break;" ).NewLine();
                                    }
                                }
                                read.Append( "case " ).AppendSourceString( h.JsonName ).Append( ":" ).NewLine();
                                if( info.PocoJsonInfo.IsECMAStandardCompliant
                                    && h.HasECMAScriptStandardJsonName
                                    && info.ECMAStandardHandlers.Contains( h ) )
                                {
                                    read.Append( "case " ).AppendSourceString( h.ECMAScriptStandardJsonName.Name ).Append( ":" ).NewLine();
                                }
                                h.GenerateRead( read, fieldName, assignOnly: true );
                                read.Append( "break;" ).NewLine();
                            }
                            else hasDefaultObject = true;
                        }
                        read.Append( "default:" ).NewLine();
                        if( hasDefaultObject )
                        {
                            read.Append( fieldName ).Append( " = (" ).Append( mainHandler.TypeInfo.GenCSharpName ).Append( ")PocoDirectory_CK.ReadNonNullNamedObject( ref r, options, name );" )
                                .NewLine()
                                .Append( "break;" );
                        }
                        else
                        {
                            read.Append( " throw new System.Text.Json.JsonException( $\"Unknown type name '{name}'.\" );" );
                        }
                        read.CloseBlock();
                        read.Append( "if( r.TokenType != System.Text.Json.JsonTokenType.EndArray ) throw new System.Text.Json.JsonException( \"Expecting end of Json Type array.\" );" ).NewLine()
                            .Append( "r.Read();" );
                        if( hasIntrinsics )
                        {
                            read.Append( "break;" ).NewLine();
                            intrinsicPart.Append( "default:" ).NewLine();
                        }
                        OpenSwitchOnNameBlock( intrinsicPart );
                        if( hasIntrinsics )
                        {
                            read.Append( "break;" )
                                .CloseBlock();
                        }
                    }
                    else
                    {
                        info.AllHandlers[0].GenerateRead( read, fieldName, false );
                    }

                    if( info.PropertyInfo.IsNullable )
                    {
                        read.CloseBlock();
                    }

                }
                else
                {
                    info.AllHandlers[0].GenerateRead( read, fieldName, false );
                }
                read.Append( "break; " )
                    .CloseBlock();
            } );
            return info;
        }


    }
}
