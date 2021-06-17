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
        JsonTypeInfo? TryRegisterInfoForEnum( Type t )
        {
            if( !t.GetExternalNames( _monitor, out string name, out string[]? previousNames ) )
            {
                return null;
            }
            var uT = Enum.GetUnderlyingType( t );
            return AllowTypeInfo( t, name, StartTokenType.Number, previousNames ).Configure(
                        ( ICodeWriter write, string variableName )
                            => write.Append( "w.WriteNumberValue( (" ).AppendCSharpName( uT ).Append( ')' ).Append( variableName ).Append( " );" ),
                        ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable )
                            =>
                        {
                            // No need to defer here: the underlying types are basic number types.
                            read.OpenBlock()
                                .Append( "var " );
                            _map[uT].GenerateRead( read, "u", true );
                            read.NewLine()
                                .Append( variableName ).Append( " = (" ).AppendCSharpName( t ).Append( ")u;" )
                                .CloseBlock();
                        } );
        }

    }
}
