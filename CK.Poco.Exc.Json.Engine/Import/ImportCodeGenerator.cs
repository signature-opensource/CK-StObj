using CK.CodeGen;
using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;

namespace CK.Setup.PocoJson
{
    /// <summary>
    /// The code reader delegate is in charge of generating the read code from a <see cref="Utf8JsonReader"/>
    /// variable named "r" and a PocoJsonReadContext variable named "rCtx" into a "variableName".
    /// </summary>
    /// <param name="write">The code writer to uses.</param>
    /// <param name="variableName">The variable name to read.</param>
    delegate void CodeReader( ICodeWriter write, string variableName );

    sealed partial class ImportCodeGenerator
    {
        readonly ITypeScope _importerType;
        readonly ITypeScopePart _readerFunctionsPart;
        readonly ExchangeableTypeNameMap _nameMap;
        readonly ICSCodeGenerationContext _generationContext;
        readonly CodeReader[] _readers;
        readonly BitArray _readerFunctions;

        public ImportCodeGenerator( ITypeScope importerType, ExchangeableTypeNameMap nameMap, ICSCodeGenerationContext generationContext )
        {
            _importerType = importerType;
            importerType.Append( @"
internal delegate T TypedReader<T>( ref System.Text.Json.Utf8JsonReader r, CK.Poco.Exc.Json.PocoJsonReadContext rCtx );

internal static void FillListOrSet<T>( ref System.Text.Json.Utf8JsonReader r, ICollection<T> c, TypedReader<T> itemReader, CK.Poco.Exc.Json.PocoJsonReadContext rCtx )
{
    if( !r.Read() ) rCtx.ReadMoreData( ref r );
    while( r.TokenType != System.Text.Json.JsonTokenType.EndArray )
    {
        c.Add( itemReader( ref r, rCtx ) );
    }
    if( !r.Read() ) rCtx.ReadMoreData( ref r );
}

internal static void FillDictionary<TKey,TValue>( ref System.Text.Json.Utf8JsonReader r, IDictionary<TKey,TValue> c, TypedReader<TKey> kR, TypedReader<TValue> vR, CK.Poco.Exc.Json.PocoJsonReadContext rCtx )
{
    if( !r.Read() ) rCtx.ReadMoreData( ref r );
    while( r.TokenType != System.Text.Json.JsonTokenType.EndArray )
    {
        if( !r.Read() ) rCtx.ReadMoreData( ref r ); // [
        c.Add( kR( ref r, rCtx ), vR( ref r, rCtx ) );
        if( !r.Read() ) rCtx.ReadMoreData( ref r );  // ]
    }
    if( !r.Read() ) rCtx.ReadMoreData( ref r );
}

internal static void FillDynamicObject<TValue>( ref System.Text.Json.Utf8JsonReader r, IDictionary<string, TValue> c, TypedReader<TValue> vR, CK.Poco.Exc.Json.PocoJsonReadContext rCtx )
{
    if( r.TokenType == System.Text.Json.JsonTokenType.StartArray )
    {
        if( !r.Read() ) rCtx.ReadMoreData( ref r );
        while( r.TokenType != System.Text.Json.JsonTokenType.EndArray )
        {
            if( !r.Read() ) rCtx.ReadMoreData( ref r );
            var k = r.GetString();
            if( !r.Read() ) rCtx.ReadMoreData( ref r );
            c.Add( k, vR( ref r, rCtx ) );
            if( !r.Read() ) rCtx.ReadMoreData( ref r );
        }
    }
    else
    {
        if( r.TokenType != System.Text.Json.JsonTokenType.StartObject ) r.ThrowJsonException( $""Expecting '{{' or '[' to start a map of string to '{typeof(TValue).ToCSharpName()}'."" );
        if( !r.Read() ) rCtx.ReadMoreData( ref r );
        while( r.TokenType != System.Text.Json.JsonTokenType.EndObject )
        {
            var k = r.GetString();
            if( !r.Read() ) rCtx.ReadMoreData( ref r );
            c.Add( k, vR( ref r, rCtx ) );
        }
    }
    if( !r.Read() ) rCtx.ReadMoreData( ref r );
}

internal static T[] ReadArray<T>( ref System.Text.Json.Utf8JsonReader r, TypedReader<T> itemReader, CK.Poco.Exc.Json.PocoJsonReadContext rCtx )
{
    var c = new List<T>();
    if( !r.Read() ) rCtx.ReadMoreData( ref r );
    while( r.TokenType != System.Text.Json.JsonTokenType.EndArray )
    {
        c.Add( itemReader( ref r, rCtx ) );
    }
    if( !r.Read() ) rCtx.ReadMoreData( ref r );
    return c.ToArray();
}

delegate object ObjectReader( ref System.Text.Json.Utf8JsonReader r, CK.Poco.Exc.Json.PocoJsonReadContext rCtx );
static readonly Dictionary<string, ObjectReader> _anyReaders = new Dictionary<string, ObjectReader>();

" ).Append( "#region Read functions." ).NewLine()
                        .CreatePart( out _readerFunctionsPart ).NewLine()
                        .Append( "#endregion" ).NewLine();
            _nameMap = nameMap;
            _generationContext = generationContext;
            _readers = new CodeReader[nameMap.TypeSystem.AllNonNullableTypes.Count];
            _readerFunctions = new BitArray( nameMap.TypeSystem.AllTypes.Count );
        }

        string GetReadFunctionName( IPocoType t )
        {
            if( t.Kind == PocoTypeKind.Any )
            {
                return "CK.Poco.Exc.JsonGen.Importer.ReadAny";
            }
            // Allow reference types to be null here (oblivious NRT mode).
            // The function is bound to the non null type, but it handles the nullable.
            var tFunc = t.Type.IsValueType ? t : t.NonNullable;
            if( !_readerFunctions[tFunc.Index] )
            {
                _readerFunctions[tFunc.Index] = true;
                var tActual = t.Type.IsValueType ? t : t.Nullable;
                Debug.Assert( tFunc.ImplTypeName == t.ImplTypeName && tActual.ImplTypeName == t.ImplTypeName );
                _readerFunctionsPart.Append( "internal static " ).Append( t.ImplTypeName ).Append( " FRead_" ).Append( tFunc.Index )
                                    .Append( "(ref System.Text.Json.Utf8JsonReader r,CK.Poco.Exc.Json.PocoJsonReadContext rCtx)" )
                                    .OpenBlock();

                _readerFunctionsPart.Append( t.ImplTypeName ).Append( " o;" ).NewLine();
                // Use the potentially nullable reference type here to generate the actual read.
                GenerateRead( _readerFunctionsPart, tActual, "o", null );
                _readerFunctionsPart.NewLine()
                    .Append( "return o;" )
                    .CloseBlock();
            }
            return $"CK.Poco.Exc.JsonGen.Importer.FRead_{tFunc.Index}";
        }

        void GenerateRead( ICodeWriter writer, IPocoType t, string variableName, bool? requiresInit )
        {
            if( t.IsNullable )
            {
                writer.Append( "if(r.TokenType==System.Text.Json.JsonTokenType.Null)" )
                        .OpenBlock()
                        .Append( variableName ).Append( "=default;" ).NewLine()
                        .Append( "if(!r.Read()) rCtx.ReadMoreData(ref r);" )
                        .CloseBlock()
                        .Append( "else" )
                        .OpenBlock();
                DoGenerateRead( writer, t, variableName, requiresInit );
                writer.CloseBlock();
            }
            else
            {
                DoGenerateRead( writer, t, variableName, requiresInit );
            }

            static bool DefaultRequiresInit( IPocoType t )
            {
                Debug.Assert( !t.IsNullable, "This must be called only when it makes sense: nullable doesn't require any initialization." );
                return t.DefaultValueInfo.RequiresInit
                       && (t.Kind == PocoTypeKind.IPoco
                           || t.Kind == PocoTypeKind.List
                           || t.Kind == PocoTypeKind.Dictionary
                           || t.Kind == PocoTypeKind.HashSet
                           || t.Kind == PocoTypeKind.AnonymousRecord
                           || t.Kind == PocoTypeKind.Record);
            }

            void DoGenerateRead( ICodeWriter writer, IPocoType t, string variableName, bool? requiresInit )
            {
                if( requiresInit ?? DefaultRequiresInit( t.NonNullable ) )
                {
                    Debug.Assert( t.NonNullable.DefaultValueInfo.DefaultValue != null, "Since requiresInit is true." );
                    writer.Append( variableName ).Append( "=" ).Append( t.NonNullable.DefaultValueInfo.DefaultValue.ValueCSharpSource ).Append( ";" ).NewLine();
                }
                // For nullable records, we need this adapter.
                if( t.IsNullable && (t.Kind == PocoTypeKind.AnonymousRecord || t.Kind == PocoTypeKind.Record) )
                {
                    variableName = $"CommunityToolkit.HighPerformance.Extensions.NullableExtensions.DangerousGetValueOrDefaultReference( ref {variableName} )";
                }
                _readers[t.Index >> 1].Invoke( writer, variableName );
            }
        }

        public bool Run( IActivityMonitor monitor )
        {
            var pocos = new List<IPrimaryPocoType>();
            var records = new List<IRecordPocoType>(); 
            RegisterReaders( pocos, records );
            foreach( var p in pocos )
            {
                GeneratePocoSupport( monitor, _generationContext, p );
            }
            foreach( var r in records )
            {
                GenerateRecordReader( monitor, _importerType, r );
            }
            GenerateReadAny();
            return true;
        }

    }
}