using CK.Poco.Exc.Json;
using System.Text.Json;

namespace CK.Core
{
    /// <summary>
    /// This interface is automaticaly implemented on the <see cref="PocoDirectory"/>.
    /// </summary>
    public interface IPocoDirectoryJsonExportGenerated
    {
        /// <summary>
        /// Writes any Poco compliants types (types must have been registered in the Poco Type System).
        /// </summary>
        /// <param name="w">The writer.</param>
        /// <param name="o">The object to write.</param>
        /// <param name="options">The write options.</param>
        void WriteAnyJson( Utf8JsonWriter w, object? o, PocoJsonExportOptions? options = null );
    }
}
