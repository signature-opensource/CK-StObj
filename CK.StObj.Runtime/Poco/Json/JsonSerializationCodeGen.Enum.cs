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
            var uT = Enum.GetUnderlyingType( t ).GetNullableTypeTree();
            var uTHandler = _map[uT];

            var info = AllowTypeInfo( t, name, previousNames );
            return info.Configure(
                        ( ICodeWriter write, string variableName )
                            => write.Append( "w.WriteNumberValue( (" ).Append( uTHandler.GenCSharpName ).Append( ')' ).Append( variableName ).Append( " );" ),
                        ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable )
                            =>
                        {
                            // No need to defer here: the underlying types are basic number types.
                            read.OpenBlock()
                                .Append( "var " );
                            uTHandler.GenerateRead( read, "u", true );
                            read.NewLine()
                                .Append( variableName ).Append( " = (" ).Append( info.GenCSharpName ).Append( ")u;" )
                                .CloseBlock();
                        } );
        }

    }
}
