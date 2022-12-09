namespace CK.Setup.PocoJson
{
    /// <summary>
    /// Exposes the Json names of the exchanged types once they have been computed
    /// and Json serialization code has been generated.
    /// </summary>
    public interface IPocoJsonGeneratorService
    {
        /// <summary>
        /// Gets the Json names of the Poco types used by the Json exchange services
        /// when it is ready to be used.
        /// <para>
        /// This map MAY contain less exchangeable types than the type system has types
        /// (it's not the case today: all <see cref="IPocoType"/> that are <see cref="IPocoType.IsExchangeable"/>
        /// are handled and no Json specific mechanism that can remove some of them is implemented).
        /// </para>
        /// </summary>
        ExchangeableTypeNameMap? JsonNames { get; }
    }
}
