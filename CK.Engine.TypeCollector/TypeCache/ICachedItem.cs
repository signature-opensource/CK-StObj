using System.Collections.Immutable;
using System.Reflection;
using System.Text;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Generalizes <see cref="ICachedType"/> and <see cref="ICachedMember"/>.
/// </summary>
public interface ICachedItem
{
    /// <summary>
    /// Gets the name of this type or member.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the custom attributes data.
    /// Instantiating attributes is more expensive that exploiting the <see cref="CustomAttributeData"/>
    /// but it requires more work and guards in the attribute constructor (if any) must be replicated.
    /// <para>
    /// The <see cref="GlobalTypeCache"/> doesn't cache attribute instances, only the data.
    /// Attributes must be cached in a contextual cache as the engine use stateful and engine-implemented
    /// attribute surrogates.
    /// </para>
    /// </summary>
    ImmutableArray<CustomAttributeData> CustomAttributes { get; }

    StringBuilder Write( StringBuilder b );
}
