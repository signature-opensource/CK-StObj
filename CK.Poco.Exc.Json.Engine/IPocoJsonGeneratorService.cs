namespace CK.Setup.PocoJson
{
    /// <summary>
    /// Exposes the Json names. This service is available after <see cref="IPocoTypeSystemBuilder.Lock()"/>
    /// has been locked.
    /// </summary>
    public interface IPocoJsonGeneratorService
    {
        /// <summary>
        /// Gets the Json names of the Poco types.
        /// <para>
        /// This map is bound to the <see cref="IPocoTypeSetManager.AllSerializable"/> and uses
        /// the standard names described by <see cref="PocoTypeNameMap"/>.
        /// </para>
        /// </summary>
        PocoTypeNameMap JsonNames { get; }
    }
}
