using System;
using System.Text.Json;

namespace CK.Core
{
    /// <summary>
    /// Enables <see cref="Utf8JsonReader"/> to be refilled with data.
    /// This interface is not <see cref="IDisposable"/> but its implementations
    /// like <see cref="Utf8JsonStreamReader"/> often needs to be.
    /// <para>
    /// The pattern to use it is to replace all calls to <creader.>Read()</c> with:
    /// <code>
    /// if( !reader.Read() ) dataProvider.ReadMoreData( ref reader );
    /// </code>
    /// And all <c>reader.Skip()</c> calls with:
    /// <code>
    /// if( !reader.TrySkip() ) dataProvider.SkipMoreData( ref reader );
    /// </code>
    /// </para>
    /// </summary>
    public interface IUtf8JsonReaderDataProvider
    {
        /// <summary>
        /// Method to call whenever <see cref="Utf8JsonReader.Read()"/> returns false.
        /// </summary>
        /// <param name="reader">The reader for which more data is needed.</param>
        void ReadMoreData( ref Utf8JsonReader reader );

        /// <summary>
        /// Method to call whenever <see cref="Utf8JsonReader.TrySkip()"/> returns false.
        /// </summary>
        /// <param name="reader">The reader for which more data is needed.</param>
        void SkipMoreData( ref Utf8JsonReader reader );
    }
}
