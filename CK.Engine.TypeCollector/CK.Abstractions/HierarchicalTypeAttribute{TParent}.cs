using System;

namespace CK.Core;

/// <summary>
/// Marks a type as being the child of another <see cref="HierarchicalTypeAttribute{TParent}"/> or <see cref="HierarchicalTypeRootAttribute"/>.
/// </summary>
/// <typeparam name="TParent"></typeparam>
[AttributeUsage( AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct, AllowMultiple = false, Inherited = false )]
public sealed class HierarchicalTypeAttribute<TParent> : Attribute
{
}
