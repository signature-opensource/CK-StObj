using CK.CodeGen;
using System;

namespace CK.Setup.PocoJson;

sealed class BasicWriter : JsonCodeWriter
{
    readonly Action<ICodeWriter, string> _rawWrite;

    public BasicWriter( ExportCodeWriterMap map, Action<ICodeWriter,string> rawWrite )
        : base( map )
    {
        _rawWrite = rawWrite;
    }

    public override void RawWrite( ICodeWriter writer, string variableName ) => _rawWrite( writer, variableName );
}
