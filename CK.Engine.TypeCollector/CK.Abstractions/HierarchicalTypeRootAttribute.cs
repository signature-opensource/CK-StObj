using System;

namespace CK.Core;

/// <summary>
/// Marks a type as being the root of a hierarchy. <see cref="HierarchicalTypeAttribute{TParent}"/> is used to mark
/// "children" types. This doesn't imply any direct relationships (via properties or methods), these attributes
/// simply describes a parent-child logical relationships between types.
/// <para>
/// The forest of types defined by these attributes can be used with different semantics. Typical usage is to organize a
/// graph of objects as a tree for display or navigation purposes.
/// </para>
/// </summary>
[AttributeUsage( AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct, AllowMultiple = false, Inherited = false )]
public sealed class HierarchicalTypeRootAttribute : Attribute
{
}
