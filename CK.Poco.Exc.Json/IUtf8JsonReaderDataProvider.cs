using System.Text.Json;

namespace CK.Core
{
    /// <summary>
    /// Enables <see cref="Utf8JsonReader"/> to be refilled with data.
    /// <para>
    /// The pattern to use it is to replace all <c>Read()</c> calls with:
    /// <code>
    /// if( !reader.Read() ) dataProvider.NeedMoreData( ref reader );
    /// </code>
    /// And all <c>Skip()</c> calls with:
    /// <code>
    /// if( !reader.TrySkip() ) dataProvider.NeedMoreData( ref reader, true );
    /// </code>
    /// </para>
    /// </summary>
    public interface IUtf8JsonReaderDataProvider
    {
        /// <summary>
        /// Method to call whenever <see cref="Utf8JsonReader.Read()"/> or <see cref="Utf8JsonReader.TrySkip()"/>
        /// returns false.
        /// </summary>
        /// <param name="reader">The reader for which more data is needed.</param>
        /// <param name="skip">True to skip rather only read one token.</param>
        void NeedMoreData( ref Utf8JsonReader reader, bool skip = false );
    }
}
