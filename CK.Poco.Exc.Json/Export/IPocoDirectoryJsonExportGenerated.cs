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
        /// <para>
        /// This is a low-level method:
        /// <list type="bullet">
        ///     <item>The <paramref name="writer"/> should have been initialized with the context's <see cref="PocoJsonExportOptions.WriterOptions"/>.</item>
        ///     <item>The <see cref="Utf8JsonWriter.Flush()"/> is not called.</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="o">The object to write.</param>
        /// <param name="context">The writer context.</param>
        /// <returns>True if the object has been written, false it it has been filtered out by <see cref="PocoJsonExportOptions.TypeFilterName"/>.</returns>
        bool WriteAnyJson( Utf8JsonWriter writer, object? o, PocoJsonWriteContext context );

        /// <summary>
        /// Writes a <see cref="IPoco"/> (that can be null) and returns true if "null" or the non null Poco has been written.
        /// <para>
        /// This is a low-level method:
        /// <list type="bullet">
        ///     <item>The <paramref name="writer"/> should have been initialized with the context's <see cref="PocoJsonExportOptions.WriterOptions"/>.</item>
        ///     <item>The <see cref="Utf8JsonWriter.Flush()"/> is not called.</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="o">The Poco to write.</param>
        /// <param name="context">The writer context.</param>
        /// <param name="withType">
        /// When true, a 2-cells array contains the Poco's name first and then the Poco's value.
        /// When false, the Poco's value object is directly written.
        /// <para>
        /// This overrides (for the root object only), the <see cref="PocoJsonExportOptions.TypeLess"/>
        /// option.
        /// </para>
        /// </param>
        /// <returns>True if this Poco has been written, false it it has been filtered out by <see cref="PocoJsonExportOptions.TypeFilterName"/>.</returns>
        bool WriteJson( Utf8JsonWriter writer, IPoco? o, PocoJsonWriteContext context, bool withType );
    }
}
