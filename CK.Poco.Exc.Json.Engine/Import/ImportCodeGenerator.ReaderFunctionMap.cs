using CK.CodeGen;
using CK.Core;
using System.Linq;

namespace CK.Setup.PocoJson
{
    sealed partial class ImportCodeGenerator
    {
        sealed class ReaderFunctionMap
        {
            readonly string?[] _names;
            readonly ReaderMap _readerMap;
            readonly ITypeScopePart _readerFunctionsPart;

            public ReaderFunctionMap( IPocoTypeNameMap nameMap, ReaderMap readerMap, ITypeScopePart readerFunctionsPart )
            {
                _names = new string[nameMap.TypeSystem.AllTypes.Count];
                _readerMap = readerMap;
                _readerFunctionsPart = readerFunctionsPart;
            }

            public string GetReadFunctionName( IPocoType t )
            {
                var name = _names[t.Index];
                if( name == null )
                {
                    if( t.Kind == PocoTypeKind.Any )
                    {
                        name = "CK.Poco.Exc.JsonGen.Importer.ReadAny";
                    }
                    else
                    {
                        if( t is ISecondaryPocoType sec )
                        {
                            name = GetReadFunctionName( sec.PrimaryPocoType );
                        }
                        else
                        {
                            // Map to regular type.
                            Throw.DebugAssert( "No abstract read-only collection here.", t.RegularType != null );
                            var target = t.RegularType;
                            Throw.DebugAssert( target.IsNullable == t.IsNullable );
                            name = _names[target.Index];
                            if( name == null )
                            {
                                name = GenerateReadFunction( t );
                            }
                        }
                    }
                    _names[t.Index] = name;
                }
                return name;
            }

            string GenerateReadFunction( IPocoType t )
            {
                _readerFunctionsPart.Append( "internal static " ).Append( t.ImplTypeName ).Append( " FRead_" ).Append( t.Index )
                                                                .Append( "(ref System.Text.Json.Utf8JsonReader r,CK.Poco.Exc.Json.PocoJsonReadContext rCtx)" )
                                                                .OpenBlock();
                _readerFunctionsPart.Append( t.ImplTypeName ).Append( " o;" ).NewLine();
                _readerMap.GenerateRead( _readerFunctionsPart, t, "o", requiresInit: true );
                _readerFunctionsPart.NewLine()
                    .Append( "return o;" );
                _readerFunctionsPart.CloseBlock();
                return $"CK.Poco.Exc.JsonGen.Importer.FRead_{t.Index}";
            }
        }
    }
}
