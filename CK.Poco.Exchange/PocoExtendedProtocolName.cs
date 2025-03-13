using System;
using System.Diagnostics.CodeAnalysis;

namespace CK.Core;

/// <summary>
/// Formalized extended protocol name: "BaseProtocolName[Options]".
/// <para>
/// Currently there is no inner model for Options but the idea is that it should be
/// a Json array with simple elements or a be like a connection string. 
/// </para>
/// </summary>
public sealed class PocoExtendedProtocolName
{
    readonly string _protocol;
    readonly string _baseName;

    /// <summary>
    /// Gets the protocol base name.
    /// </summary>
    public string BaseName => _baseName;

    /// <summary>
    /// Gets the options part including the '[' and ']'.
    /// </summary>
    public ReadOnlySpan<char> Options => _protocol.AsSpan( _baseName.Length );

    /// <summary>
    /// Gets the full protocol name.
    /// </summary>
    /// <returns></returns>
    public override string ToString() => _protocol;

    PocoExtendedProtocolName( string protocol, string baseName )
    {
        _protocol = protocol;
        _baseName = baseName;
    }

    /// <summary>
    /// Tries to parse a protocol name.
    /// </summary>
    /// <param name="protocol">The full protocol name.</param>
    /// <param name="result">The parsed result.</param>
    /// <returns>true if protocol was successfully parsed; otherwise, false.</returns>
    public static bool TryParse( string protocol, [NotNullWhen( true )] out PocoExtendedProtocolName? result )
    {
        Throw.CheckNotNullOrEmptyArgument( protocol );
        result = null;
        if( protocol[protocol.Length - 1] != ']' ) return false;
        int idx = protocol.IndexOf( '[' );
        if( idx <= 0 ) return false;
        result = new PocoExtendedProtocolName( protocol, protocol.Substring( 0, idx ) );
        return true;
    }

}
