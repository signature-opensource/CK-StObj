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
                _names = new string[nameMap.TypeSet.Count];
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
                            _readerFunctionsPart.Append( "internal static " ).Append( t.ImplTypeName ).Append( " FRead_" ).Append( t.Index )
                                                .Append( "(ref System.Text.Json.Utf8JsonReader r,CK.Poco.Exc.Json.PocoJsonReadContext rCtx)" )
                                                .OpenBlock();
                            _readerFunctionsPart.Append( t.ImplTypeName ).Append( " o;" ).NewLine();
                            _readerMap.GenerateRead( _readerFunctionsPart, t, "o", true );
                            _readerFunctionsPart.NewLine()
                                .Append( "return o;" );
                            _readerFunctionsPart.CloseBlock();
                            name = $"CK.Poco.Exc.JsonGen.Importer.FRead_{t.Index}";
                        }
                    }
                    _names[t.Index] = name;
                }
                return name;
            }

        }
    }
}
