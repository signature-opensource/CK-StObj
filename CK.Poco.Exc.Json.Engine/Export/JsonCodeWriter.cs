using CK.CodeGen;
using CK.Core;
using System.Linq;

namespace CK.Setup.PocoJson;

/// <summary>
/// Writer code relies on a <see cref="System.Text.Json.Utf8JsonWriter"/> named "w" and a
/// PocoJsonWriteContext variable named "wCtx".
/// </summary>
abstract class JsonCodeWriter : ExportCodeWriter
{
    public JsonCodeWriter( ExportCodeWriterMap map )
        : base( map, false )
    {
    }

    public JsonCodeWriter( ExportCodeWriterMap map, string key )
        : base( map, key, false )
    {
    }

    public override void WriteNull( ICodeWriter writer ) => writer.Append( "w.WriteNullValue();" );
}
