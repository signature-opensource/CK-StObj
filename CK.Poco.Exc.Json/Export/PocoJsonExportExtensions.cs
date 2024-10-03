using CK.Poco.Exc.Json;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Text.Json;

namespace CK.Core;

/// <summary>
/// Provides read from Json extension methods.
/// </summary>
public static class PocoJsonExportExtensions
{
    /// <summary>
    /// Exports this Poco in Json according to the export options.
    /// This returns an empty string when filtered out because of <see cref="PocoJsonExportOptions.TypeFilterName"/>). 
    /// </summary>
    /// <param name="this">This Poco.</param>
    /// <param name="options">The Json export options.</param>
    /// <param name="withType">
    /// When true, a 2-cells array contains the Poco's name first and then the Poco's value.
    /// When false, the Poco's value object is directly written.
    /// <para>
    /// This overrides (for the root object only), the <see cref="PocoJsonExportOptions.TypeLess"/>
    /// option.
    /// </para>
    /// </param>
    /// <returns>The Json string or an empty string.</returns>
    public static string ToString( this IPoco @this, PocoJsonExportOptions options, bool withType = false )
    {
        var m = new ArrayBufferWriter<byte>();
        WriteJson( @this, m, withType, options );
        return Encoding.UTF8.GetString( m.WrittenMemory.Span );
    }

    #region PocoDirectory.WriteAnyJson to Utf8JsonWriter and PocoJsonWriteContext (relay to generated code), IBufferWriter<byte> and Stream.
    /// <inheritdoc cref="IPocoDirectoryJsonExportGenerated.WriteAnyJson(Utf8JsonWriter, object?, PocoJsonWriteContext?)"/>
    public static bool WriteAnyJson( this PocoDirectory @this, Utf8JsonWriter writer, object? o, PocoJsonWriteContext context )
    {
        return ((IPocoDirectoryJsonExportGenerated)@this).WriteAnyJson( writer, o, context );
    }

    /// <summary>
    /// Writes any Poco compliants types (types must have been registered in the Poco Type System).
    /// </summary>
    /// <param name="this">This Poco directory.</param>
    /// <param name="output">The target output.</param>
    /// <param name="o">The object to write.</param>
    /// <param name="options">Optional export options: defaults to <see cref="PocoJsonExportOptions.Default"/>.</param>
    /// <returns>True if this object has been written, false it it has been filtered out by <see cref="PocoJsonExportOptions.TypeFilterName"/>.</returns>
    public static bool WriteAnyJson( this PocoDirectory @this, IBufferWriter<byte> output, object? o, PocoJsonExportOptions? options = null )
    {
        var pW = (IPocoDirectoryJsonExportGenerated)@this;
        using( var wCtx = new PocoJsonWriteContext( @this, options ) )
        using( var w = new Utf8JsonWriter( output, wCtx.Options.WriterOptions ) )
        {
            // Utf8JsonWriter.Dispose calls its Flush().
            return pW.WriteAnyJson( w, o, wCtx );
        }
    }

    /// <inheritdoc cref="WriteJson(PocoDirectory, IBufferWriter{byte}, IPoco?, bool, PocoJsonExportOptions?)"/>
    public static bool WriteAnyJson( this PocoDirectory @this, Stream output, object? o, PocoJsonExportOptions? options = null )
    {
        var pW = (IPocoDirectoryJsonExportGenerated)@this;
        using( var wCtx = new PocoJsonWriteContext( @this, options ) )
        using( var w = new Utf8JsonWriter( output, wCtx.Options.WriterOptions ) )
        {
            // Utf8JsonWriter.Dispose calls its Flush().
            return pW.WriteAnyJson( w, o, wCtx );
        }
    }

    #endregion

    #region PocoDirectory.WriteJson for nullable IPoco to Utf8JsonWriter and PocoJsonWriteContext (relay to generated code), IBufferWriter<byte> and Stream.
    /// <inheritdoc cref="IPocoDirectoryJsonExportGenerated.WriteJson(Utf8JsonWriter, IPoco?, PocoJsonWriteContext, bool)"/>
    public static bool WriteJson( this PocoDirectory @this, Utf8JsonWriter writer, IPoco? o, PocoJsonWriteContext context, bool withType )
    {
        return ((IPocoDirectoryJsonExportGenerated)@this).WriteJson( writer, o, context, withType );
    }

    /// <summary>
    /// Writes a <see cref="IPoco"/> (that can be null) and returns whether either "null"
    /// or the Poco has been written (or filtered out because of <see cref="PocoJsonExportOptions.TypeFilterName"/>).
    /// </summary>
    /// <param name="this">This Poco directory.</param>
    /// <param name="output">The target output.</param>
    /// <param name="o">The object to write.</param>
    /// <param name="withType">
    /// When true, a 2-cells array contains the Poco's name first and then the Poco's value.
    /// When false, the Poco's value object is directly written.
    /// <para>
    /// This overrides (for the root object only), the <see cref="PocoJsonExportOptions.TypeLess"/>
    /// option.
    /// </para>
    /// </param>
    /// <param name="options">Optional export options: defaults to <see cref="PocoJsonExportOptions.Default"/>.</param>
    /// <returns>True if this Poco has been written, false it it has been filtered out by <see cref="PocoJsonExportOptions.TypeFilterName"/>.</returns>
    public static bool WriteJson( this PocoDirectory @this, IBufferWriter<byte> output, IPoco? o, bool withType = false, PocoJsonExportOptions? options = null )
    {
        var pW = (IPocoDirectoryJsonExportGenerated)@this;
        using( var wCtx = new PocoJsonWriteContext( @this, options ) )
        using( var w = new Utf8JsonWriter( output, wCtx.Options.WriterOptions ) )
        {
            // Utf8JsonWriter.Dispose calls its Flush().
            return pW.WriteJson( w, o, wCtx, withType );
        }
    }

    /// <inheritdoc cref="WriteJson(PocoDirectory, IBufferWriter{byte}, IPoco?, bool, PocoJsonExportOptions?)"/>
    public static bool WriteJson( this PocoDirectory @this, Stream output, IPoco? o, bool withType = false, PocoJsonExportOptions? options = null )
    {
        var pW = (IPocoDirectoryJsonExportGenerated)@this;
        using( var wCtx = new PocoJsonWriteContext( @this, options ) )
        using( var w = new Utf8JsonWriter( output, wCtx.Options.WriterOptions ) )
        {
            // Utf8JsonWriter.Dispose calls its Flush().
            return pW.WriteJson( w, o, wCtx, withType );
        }
    }

    #endregion

    /// <inheritdoc cref="PocoJsonExportSupport.IWriter.WriteJson(Utf8JsonWriter, PocoJsonWriteContext,bool)"/>
    public static bool WriteJson( this IPoco @this, Utf8JsonWriter writer, PocoJsonWriteContext context, bool withType )
    {
        Throw.CheckNotNullArgument( @this );
        return ((PocoJsonExportSupport.IWriter)@this).WriteJson( writer, context, withType );
    }

    #region IPoco.WriteJson to Utf8JsonWriter and PocoJsonWriteContext (relay to generated code), IBufferWriter<byte> and Stream.

    /// <inheritdoc cref="PocoJsonExportSupport.IWriter.WriteJson(Utf8JsonWriter, PocoJsonWriteContext)"/>
    public static bool WriteJson( this IPoco @this, Utf8JsonWriter writer, PocoJsonWriteContext context )
    {
        Throw.CheckNotNullArgument( @this );
        return ((PocoJsonExportSupport.IWriter)@this).WriteJson( writer, context );
    }

    /// <summary>
    /// Writes this IPoco as Json (with or without its type) if it is allowed by the context's <see cref="PocoJsonExportOptions.TypeFilterName"/>.
    /// </summary>
    /// <param name="this">This Poco.</param>
    /// <param name="output">The target.</param>
    /// <param name="withType">
    /// When true, a 2-cells array contains the Poco's name first and then the Poco's value.
    /// When false, the Poco's value object is directly written.
    /// <para>
    /// This overrides (for the root object only), the <see cref="PocoJsonExportOptions.TypeLess"/>
    /// option.
    /// </para>
    /// </param>
    /// <param name="options">Optional export options: defaults to <see cref="PocoJsonExportOptions.Default"/>.</param>
    /// <returns>True if this Poco has been written, false it it has been filtered out by <see cref="PocoJsonExportOptions.TypeFilterName"/>.</returns>
    public static bool WriteJson( IPoco @this, IBufferWriter<byte> output, bool withType = false, PocoJsonExportOptions? options = null )
    {
        Throw.CheckNotNullArgument( @this );
        using( var wCtx = new PocoJsonWriteContext( ((IPocoGeneratedClass)@this).Factory.PocoDirectory, options ) )
        using( var w = new Utf8JsonWriter( output, wCtx.Options.WriterOptions ) )
        {
            // Utf8JsonWriter.Dispose calls its Flush().
            var pW = ((PocoJsonExportSupport.IWriter)@this);
            return withType ? pW.WriteJson( w, wCtx, true ) : pW.WriteJson( w, wCtx );
        }
    }

    /// <inheritdoc cref="WriteJson(IPoco, IBufferWriter{byte}, bool, PocoJsonExportOptions?)"/>
    public static bool WriteJson( this IPoco @this, Stream output, bool withType = false, PocoJsonExportOptions? options = null )
    {
        Throw.CheckNotNullArgument( @this );
        using( var wCtx = new PocoJsonWriteContext( ((IPocoGeneratedClass)@this).Factory.PocoDirectory, options ) )
        using( var w = new Utf8JsonWriter( output, wCtx.Options.WriterOptions ) )
        {
            // Utf8JsonWriter.Dispose calls its Flush().
            return ((PocoJsonExportSupport.IWriter)@this).WriteJson( w, wCtx, withType );
        }
    }
    #endregion

    /// <summary>
    /// Throws a <see cref="JsonException"/>.
    /// </summary>
    /// <param name="writer">This writer.</param>
    /// <param name="message">The exception message.</param>
    [DoesNotReturn]
    public static void ThrowJsonException( this Utf8JsonWriter writer, string message )
    {
        throw new JsonException( $"{message} - {writer.BytesCommitted} committed bytes, current depth is {writer.CurrentDepth}." );
    }

    /// <summary>
    /// Throws a <see cref="JsonException"/> with "Unexpected null value for a non nullable." message.
    /// </summary>
    /// <param name="writer">This writer.</param>
    [DoesNotReturn]
    public static void ThrowJsonNullWriteException( this Utf8JsonWriter writer )
    {
        ThrowJsonException( writer, "Unexpected null value for a non nullable." );
    }

}
