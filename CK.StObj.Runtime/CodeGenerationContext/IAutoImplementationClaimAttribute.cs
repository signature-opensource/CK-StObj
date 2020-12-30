using CK.Core;

namespace CK.Setup
{

    /// <summary>
    /// Interface marker for attributes that mark property or method that can be automatically implemented.
    /// This interface states that there is a way to implement it, but does not provide it: this is the reusable
    /// equivalent of <see cref="AutoImplementationClaimAttribute"/>.
    /// </summary>
    /// <remarks>
    /// See <see cref="IAutoImplementorMethod"/>, <see cref="IAutoImplementorProperty"/> or <see cref="ICSCodeGeneratorType"/>
    /// that are able to actually implement methods and properties.
    /// <para>
    /// Attributes that support those interfaces can directly provide an implementation: when an attribute only support
    /// this <see cref="IAutoImplementationClaimAttribute"/> marker, the implementation must be provided by other means.
    /// </para>
    /// </remarks>
    public interface IAutoImplementationClaimAttribute
    {
    }
}
