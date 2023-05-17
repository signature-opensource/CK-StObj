using CK.Setup;

namespace CK.Core
{
    /// <summary>
    /// Base class for a end point.
    /// The specialized class must be decorated with <see cref="EndpointDefinitionAttribute"/>.
    /// </summary>
    [CKTypeDefiner]
    public abstract class EndpointDefinition : IRealObject
    {
    }

}
