
namespace CK.StObj.Engine.Tests.Endpoint
{
    /// <inheritdoc cref="IFakeTenantInfo"/>
    /// <remarks>
    /// Ambient services are NOT automatically resolved:
    /// - Their resolution is explicitly registered via a factory (since they are scoped) by the DIContainerDefinition.ConfigureServiceEndpoint method
    /// - OR they are resolved from the AmbientServiceHub scoped data (for backend contexts)
    /// - OR they are resolved to their default value (by their IAmbientServiceDefaultProvider singleton companion) for Front endpoint.
    /// <para>
    /// We don't need any specific constructor for them, the constructors are what they are and there can be multiple constructors!
    /// The "single constructor AutoService" limitation doesn't apply to them.
    /// </para>
    /// </remarks>
    public class FakeTenantInfo : IFakeTenantInfo
    {
        public FakeTenantInfo( string name )
        {
            Name = name;
        }

        public FakeTenantInfo( string name, string alternateName )
        {
            Name = name;
            AlternateName = alternateName;
        }

        public string Name { get; }

        public string? AlternateName { get; }

        public override string ToString() => Name;

    }
}
