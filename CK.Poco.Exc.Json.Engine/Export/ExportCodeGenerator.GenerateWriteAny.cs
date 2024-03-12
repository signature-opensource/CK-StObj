using CK.CodeGen;
using CK.Core;
using System.ComponentModel.Design;
using System.Linq;

namespace CK.Setup.PocoJson
{
    sealed partial class ExportCodeGenerator
    {
        // Step 3: Generating the WriteAny that routes any object to its registered Oblivious type.
        //         This is basically a big switch case on the object.GetType() except that it is broken
        //         into smaller pieces for readability (and hopefully better performance if there are a lot of types).
        void GenerateWriteAny()
        {
            _exporterType
                .GeneratedByComment()
                .Append( @"
internal static void WriteAny( System.Text.Json.Utf8JsonWriter w, object o, CK.Poco.Exc.Json.PocoJsonWriteContext wCtx )
{
    var t = o.GetType();
    if( t.IsValueType )
    {
        if( t.IsEnum )
        {
            switch( o )
            {
                " ).CreatePart( out var enumCases ).Append( @"
                default: w.ThrowJsonException( $""Unregistered enumeration type: {t.ToCSharpName(false)}"" ); break;
            }
        }
        else if( t.Name.StartsWith( ""ValueTuple`"", StringComparison.Ordinal ) && t.Namespace == ""System"" )
        {
            switch( o )
            {
                " ).CreatePart( out var valueTupleCases ).Append( @"
                default: w.ThrowJsonException( $""Unregistered ValueTuple: {t.ToCSharpName(false)}"" ); break;
            }
        }
        else switch( o )
        {
            " ).CreatePart( out var basicValueTypeCases ).Append( @"
            " ).CreatePart( out var namedRecordCases ).Append( @"
            default: w.ThrowJsonException( $""Unregistered value type: {t.ToCSharpName(false)}"" ); break;
        }
    }
    else
    {
        switch( o )
        {
            " ).CreatePart( out var basicRefTypeCases ).Append( @"
            case IPoco:
            {
                switch( o )
                {
                    " ).CreatePart( out var pocoCases ).Append( @"
                }
                break;
            }
            case Array:
            {
                switch( o )
                {
                    " ).CreatePart( out var arrayCases ).Append( @"
                    default: w.ThrowJsonException( $""Unregistered array type: {t.ToCSharpName(false)}"" ); break;
                }
                break;
            }
            " ).CreatePart( out var collectionCases ).Append( @"
            default: w.ThrowJsonException( $""Unregistered type: {t.ToCSharpName(false)}"" ); break;
        }
    }
}" );
            // Builds the different sorters for cases that must be ordered: arrays and collections
            // only since these are the only reference types except the basic ones (that 
            // is currently: the 'string', the Globalization reference types MCString, CodeString, Normalized & ExtendedCultureInfo).
            var arrays = new ObliviousReferenceTypeSorter();
            var collections = new ObliviousReferenceTypeSorter();
            // Among the Globalization reference types, only the Normalized & ExtendedCultureInfo have an issue:
            // the specialized NormalizedCultureInfo must appear before ExtendedCultureInfo.
            // We don't use a ObliviousReferenceTypeSorter for 2 types here.
            IPocoType? extendedCultureInfo = null;
            IPocoType? normalizedCultureInfo = null;

            // Non nullable final types that are serializables: these are the types that need to be written.
            foreach( var t in _nameMap.TypeSystem.NonNullableFinalTypes.Where( _nameMap.TypeSet.Contains ) )
            {
                switch( t.Kind )
                {
                    case PocoTypeKind.Basic:
                        {
                            if( t.Type.IsValueType )
                            {
                                WriteCase( basicValueTypeCases, t );
                            }
                            else
                            {
                                if( t.Type == typeof( string ) )
                                {
                                    WriteCase( basicRefTypeCases, t );
                                }
                                else
                                {
                                    if( t.Type == typeof( NormalizedCultureInfo ) )
                                    {
                                        normalizedCultureInfo = t;
                                    }
                                    else if( t.Type == typeof( ExtendedCultureInfo ) )
                                    {
                                        extendedCultureInfo = t;
                                    }
                                    else
                                    {
                                        Throw.DebugAssert( t.Type == typeof( MCString ) || t.Type == typeof( CodeString ) );
                                        WriteCase( basicRefTypeCases, t );
                                    }
                                }
                            }
                            break;
                        }

                    case PocoTypeKind.Enum:
                        {
                            WriteCase( enumCases, t );
                            break;
                        }

                    case PocoTypeKind.Array:
                        {
                            arrays.Add( t );
                            break;
                        }

                    case PocoTypeKind.PrimaryPoco:
                        {
                            WriteCase( pocoCases, t );
                            break;
                        }
                    case PocoTypeKind.List:
                    case PocoTypeKind.HashSet:
                    case PocoTypeKind.Dictionary:
                        collections.Add( t );
                        break;
                    case PocoTypeKind.AnonymousRecord:
                        {
                            // Switch case doesn't work with (tuple, syntax).
                            WriteCase( valueTupleCases, t, t.Type.ToCSharpName( useValueTupleParentheses: false ) );
                            break;
                        }
                    case PocoTypeKind.Record:
                        {
                            WriteCase( namedRecordCases, t );
                            break;
                        }
                    default:
                        Throw.NotSupportedException( t.ToString() );
                        break;
                }
            }

            foreach( var t in arrays.SortedTypes ) WriteCase( arrayCases, t );
            foreach( var t in collections.SortedTypes ) WriteCase( collectionCases, t );
            // Normalized MUST come first.
            if( normalizedCultureInfo != null ) WriteCase( basicRefTypeCases, normalizedCultureInfo );
            if( extendedCultureInfo != null ) WriteCase( basicRefTypeCases, extendedCultureInfo );
            return;

            void WriteCase( ITypeScopePart code, IPocoType t, string? typeName = null )
            {
                Throw.DebugAssert( t.IsOblivious );
                code.Append( "case " ).Append( typeName ?? t.ImplTypeName ).Append( " v: if( wCtx.RuntimeFilter.Contains(" ).Append( t.Index >> 1 ).Append( ") )" )
                    .OpenBlock()
                    .Append( "if( !wCtx.Options.TypeLess )" )
                    .OpenBlock()
                        .Append( "w.WriteStartArray();" )
                        .Append( "w.WriteStringValue(" ).AppendSourceString( _nameMap.GetName( t ) ).Append( ");" )
                    .CloseBlock();
                _writers[t.Index >> 1].Invoke( code, "v" );
                code.NewLine()
                    .Append( "if( !wCtx.Options.TypeLess ) w.WriteEndArray();" ).NewLine()
                    .CloseBlock()
                    .Append( "break;" );
            }
        }

    }
}
