namespace CK.Core
{
    /// <summary>
    /// Marker interface for ambient service.
    /// An ambient service is a scoped service. See <see cref="AmbientServiceHub"/>.
    /// </summary>
    [CKTypeDefiner]
    public interface IAmbientAutoService : IScopedAutoService
    {
    }



}
