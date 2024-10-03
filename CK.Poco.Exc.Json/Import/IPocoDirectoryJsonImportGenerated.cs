using CK.Poco.Exc.Json;
using System.Text.Json;

namespace CK.Core;

/// <summary>
/// This interface is automaticaly implemented on the <see cref="PocoDirectory"/>.
/// </summary>
public interface IPocoDirectoryJsonImportGenerated
{
    /// <summary>
    /// Reads any Poco compliants types (null, number, string, etc.).
    /// IPoco and other complex types like collections or records must (obviously) be typed.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="context">The read context.</param>
    /// <returns>The read object (null for <see cref="JsonTokenType.Null"/>).</returns>
    object? ReadAnyJson( ref Utf8JsonReader reader, PocoJsonReadContext context );
}
