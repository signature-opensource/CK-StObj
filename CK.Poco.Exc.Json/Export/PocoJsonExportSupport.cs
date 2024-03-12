using CK.Poco.Exc.Json;
using System.Text.Json;

namespace CK.Core
{
    /// <summary>
    /// Supports Json serialization for <see cref="IPoco"/> types.
    /// </summary>
    public static class PocoJsonExportSupport
    {
        /// <summary>
        /// This interface is automatically supported by IPoco.
        /// The <see cref="PocoJsonExportSupport.WriteJson"/> extension methods exposes it.
        /// </summary>
        public interface IWriter
        {
            /// <summary>
            /// Writes this IPoco as Json if it is allowed by the <see cref="PocoJsonExportOptions.TypeFilterName"/>.
            /// </summary>
            /// <param name="writer">The Json writer.</param>
            /// <param name="context">Writer context.</param>
            /// <param name="withType">
            /// When true, a 2-cells array contains the Poco's name first and then the Poco's value.
            /// When false, the Poco's value object is directly written.
            /// <para>
            /// This overrides (for the root object only), the <see cref="PocoJsonExportOptions.TypeLess"/>
            /// option.
            /// </para>
            /// </param>
            /// <returns>True if this Poco has been written, false it it has been filtered out by <see cref="PocoJsonExportOptions.TypeFilterName"/>.</returns>
            bool WriteJson( Utf8JsonWriter writer, PocoJsonWriteContext context, bool withType );

            /// <summary>
            /// Writes this IPoco as Json without its type if it is allowed by the <see cref="PocoJsonExportOptions.TypeFilterName"/>.
            /// </summary>
            /// <param name="writer">The Json writer.</param>
            /// <param name="context">Writer context.</param>
            /// <returns>True if this Poco has been written, false it it has been filtered out by <see cref="PocoJsonExportOptions.TypeFilterName"/>.</returns>
            bool WriteJson( Utf8JsonWriter writer, PocoJsonWriteContext context );
        }

    }
}
