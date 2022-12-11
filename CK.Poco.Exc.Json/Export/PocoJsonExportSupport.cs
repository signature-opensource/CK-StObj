using CK.Poco.Exc.Json;
using CK.Setup;
using System.Text.Json;

namespace CK.Core
{
    /// <summary>
    /// Supports Json serialization for <see cref="IPoco"/> types.
    /// </summary>
    [ContextBoundDelegation( "CK.Setup.PocoJson.CommonImpl, CK.Poco.Exc.Json.Engine" )]
    public static class PocoJsonExportSupport
    {
        /// <summary>
        /// This interface is automatically supported by IPoco.
        /// The <see cref="PocoJsonExportSupport.WriteJson"/> extension methods exposes it.
        /// </summary>
        public interface IWriter
        {
            /// <summary>
            /// Writes this IPoco as Json.
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
            void WriteJson( Utf8JsonWriter writer, PocoJsonWriteContext context, bool withType );

            /// <summary>
            /// Writes this IPoco as Json without its type.
            /// </summary>
            /// <param name="writer">The Json writer.</param>
            /// <param name="context">Writer context.</param>
            void WriteJson( Utf8JsonWriter writer, PocoJsonWriteContext context );
        }

        /// <summary>
        /// Writes this IPoco (that can be null) as Json.
        /// When this is null, the Json null value is written.
        /// </summary>
        /// <param name="o">This Poco (that can be null).</param>
        /// <param name="writer">The Json writer.</param>
        /// <param name="context">Writer context.</param>
        /// <param name="withType">
        /// When true (the default), a 2-cells array contains the Poco's <see cref="IPocoFactory.Name"/> first
        /// and then the Poco's value.
        /// When false, the Poco's value object is directly written.
        /// </param>
        public static void WriteJson( this IPoco? o, Utf8JsonWriter writer, PocoJsonWriteContext context, bool withType = true )
        {
            Throw.CheckNotNullArgument( writer );
            Throw.CheckNotNullArgument( context );
            if( o == null ) writer.WriteNullValue();
            else ((IWriter)o).WriteJson( writer, context, withType );
        }

    }
}
