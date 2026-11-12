using CK.CodeGen;
using CK.Core;
using System.Linq;
using System.Text.Json;

namespace CK.Setup.PocoJson;

/// <summary>
/// The code reader delegate is in charge of generating the read code from a <see cref="Utf8JsonReader"/>
/// variable named "r" and a PocoJsonReadContext variable named "rCtx" into a "variableName".
/// </summary>
/// <param name="write">The code writer to uses.</param>
/// <param name="variableName">The variable name to read.</param>
delegate void CodeReader( ICodeWriter write, string variableName );

/// <summary>
/// Before 2024-02 array was "T[]" (instead of "A(T)").
/// UnionType, CollectionType and anonymous record types are impacted.
/// </summary>
sealed class LegacyArrayName : PocoTypeNameMap
{
    readonly IPocoTypeNameMap _standard;

    public LegacyArrayName( IPocoTypeNameMap standard )
        : base( standard.TypeSet )
    {
        _standard = standard;
    }

    protected override void MakeCSharpName( IPocoType t, out string name, out string nullableName )
    {
        Throw.DebugAssert( !t.IsNullable );
        name = _standard.GetName( t );
        nullableName = _standard.GetName( t.Nullable );
    }

    protected override void MakeNamedType( INamedPocoType namedType, out string name, out string nullableName )
    {
        Throw.DebugAssert( !namedType.IsNullable );
        name = _standard.GetName( namedType );
        nullableName = _standard.GetName( namedType.Nullable );
    }

    protected override void MakeCollection( ICollectionPocoType t, out string name, out string nullableName )
    {
        if( t.Kind == PocoTypeKind.Array )
        {
            name = $"{GetName( t.ItemTypes[0] )}[]";
            nullableName = name + '?';
            return;
        }
        base.MakeCollection( t, out name, out nullableName );
    }
}

sealed partial class ImportCodeGenerator
{
    readonly ITypeScope _importerType;
    readonly ITypeScopePart _readerFunctionsPart;
    readonly IPocoTypeNameMap _nameMap;
    readonly LegacyArrayName _legacyNameMap;
    readonly ICSCodeGenerationContext _generationContext;

    public ImportCodeGenerator( ITypeScope importerType, IPocoTypeNameMap nameMap, ICSCodeGenerationContext generationContext )
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

internal static void FillListOrSetOfAny<T>( ref System.Text.Json.Utf8JsonReader r, ICollection<T> c, CK.Poco.Exc.Json.PocoJsonReadContext rCtx )
{
    if( !r.Read() ) rCtx.ReadMoreData( ref r );
    while( r.TokenType != System.Text.Json.JsonTokenType.EndArray )
    {
        c.Add( (T)ReadAny( ref r, rCtx ) );
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

internal static T[] ReadArrayOfAny<T>( ref System.Text.Json.Utf8JsonReader r, CK.Poco.Exc.Json.PocoJsonReadContext rCtx )
{
    var c = new List<T>();
    if( !r.Read() ) rCtx.ReadMoreData( ref r );
    while( r.TokenType != System.Text.Json.JsonTokenType.EndArray )
    {
        c.Add( (T)ReadAny( ref r, rCtx ) );
    }
    if( !r.Read() ) rCtx.ReadMoreData( ref r );
    return c.ToArray();
}

internal static char ReadChar( ref System.Text.Json.Utf8JsonReader r, CK.Poco.Exc.Json.PocoJsonReadContext rCtx )
{
    // see https://source.dot.net/#System.Text.Json/System/Text/Json/Serialization/Converters/Value/CharConverter.cs,cd3a8b4ef167e1e6
    Span<char> buffer = stackalloc char[6];
    int charsWritten = r.CopyString(buffer);
    if( charsWritten != 1 )
    {
         r.ThrowJsonException( ""Expected character."" );
    }
    if( !r.Read() ) rCtx.ReadMoreData( ref r );
    return buffer[0];
}

// JSON has a single number type: 1, 1.0 and 1e0 are the same value (and a JSON Schema ""integer"" accepts them all).
// The integral types read the plain integer token directly (the fast path); any other number is accepted when its
// value is integral and in the range of the type (1.5, or 300 for a byte, are refused).
internal static int ReadInt32( ref System.Text.Json.Utf8JsonReader r ) => r.TryGetInt32( out var v ) ? v : (int)ReadIntegral( ref r, int.MinValue, int.MaxValue );
internal static uint ReadUInt32( ref System.Text.Json.Utf8JsonReader r ) => r.TryGetUInt32( out var v ) ? v : (uint)ReadIntegral( ref r, uint.MinValue, uint.MaxValue );
internal static short ReadInt16( ref System.Text.Json.Utf8JsonReader r ) => r.TryGetInt16( out var v ) ? v : (short)ReadIntegral( ref r, short.MinValue, short.MaxValue );
internal static ushort ReadUInt16( ref System.Text.Json.Utf8JsonReader r ) => r.TryGetUInt16( out var v ) ? v : (ushort)ReadIntegral( ref r, ushort.MinValue, ushort.MaxValue );
internal static byte ReadByte( ref System.Text.Json.Utf8JsonReader r ) => r.TryGetByte( out var v ) ? v : (byte)ReadIntegral( ref r, byte.MinValue, byte.MaxValue );
internal static sbyte ReadSByte( ref System.Text.Json.Utf8JsonReader r ) => r.TryGetSByte( out var v ) ? v : (sbyte)ReadIntegral( ref r, sbyte.MinValue, sbyte.MaxValue );
internal static long ReadInt64( ref System.Text.Json.Utf8JsonReader r ) => r.TryGetInt64( out var v ) ? v : (long)ReadIntegral( ref r, long.MinValue, long.MaxValue );
internal static ulong ReadUInt64( ref System.Text.Json.Utf8JsonReader r ) => r.TryGetUInt64( out var v ) ? v : (ulong)ReadIntegral( ref r, ulong.MinValue, ulong.MaxValue );

static decimal ReadIntegral( ref System.Text.Json.Utf8JsonReader r, decimal min, decimal max )
{
    if( !r.TryGetDecimal( out var d ) || d != decimal.Truncate( d ) || d < min || d > max )
    {
        r.ThrowJsonException( $""Expected an integral number between {min} and {max}."" );
    }
    return d;
}

delegate object ObjectReader( ref System.Text.Json.Utf8JsonReader r, CK.Poco.Exc.Json.PocoJsonReadContext rCtx );
// The reader of a typed value and the type index (checked against the RuntimeFilter).
static readonly Dictionary<string, (ObjectReader Reader, int Index)> _anyReaders;

" ).Append( "#region Read functions." ).NewLine()
                    .CreatePart( out _readerFunctionsPart ).NewLine()
                    .Append( "#endregion" ).NewLine();
        _nameMap = nameMap;
        _legacyNameMap = new LegacyArrayName( nameMap );
        _generationContext = generationContext;
    }

    public bool Run( IActivityMonitor monitor )
    {
        var readerMap = new ReaderMap( _nameMap );
        var functionMap = new ReaderFunctionMap( _nameMap, readerMap, _readerFunctionsPart );
        readerMap.Initialize( functionMap );
        foreach( var p in _nameMap.TypeSet.NonNullableTypes.OfType<IPrimaryPocoType>() )
        {
            GeneratePocoSupport( monitor, _generationContext, p, readerMap );
        }
        foreach( var r in _nameMap.TypeSet.NonNullableTypes.OfType<IRecordPocoType>() )
        {
            GenerateRecordReader( _importerType, r, readerMap, functionMap );
        }
        GenerateReadAny( functionMap );
        SupportPocoDirectoryJsonImportGenerated( monitor );
        return true;
    }

    void SupportPocoDirectoryJsonImportGenerated( IActivityMonitor monitor )
    {
        ITypeScope pocoDirectory = _generationContext.Assembly.Code.Global.FindOrCreateAutoImplementedClass( monitor, typeof( PocoDirectory ) );
        pocoDirectory.Definition.BaseTypes.Add( new ExtendedTypeName( "CK.Core.IPocoDirectoryJsonImportGenerated" ) );
        var read = pocoDirectory.CreateFunction( "object? CK.Core.IPocoDirectoryJsonImportGenerated.ReadAnyJson( " +
                                                    "ref System.Text.Json.Utf8JsonReader r, " +
                                                    "Poco.Exc.Json.PocoJsonReadContext context)" );
        read.Append( "Throw.CheckNotNullArgument( context );" ).NewLine()
            .Append( "return " ).Append( _importerType.FullName )
                                .Append( ".ReadAny( ref r, context );" );
    }
}
