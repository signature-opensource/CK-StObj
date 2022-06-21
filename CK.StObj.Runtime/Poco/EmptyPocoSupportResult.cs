using CK.CodeGen;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace CK.Setup
{
    /// <summary>
    /// Null object pattern implementation: the singleton <see cref="Default"/> can be
    /// used instead of null reference.
    /// </summary>
    public class EmptyPocoSupportResult : IPocoSupportResult
    {
        /// <summary>
        /// Gets an empty <see cref="IPocoSupportResult"/>.
        /// </summary>
        public static IPocoSupportResult Default { get; } = new EmptyPocoSupportResult();

        class PocoClassEmpty : IPocoClassSupportResult
        {
            public IReadOnlyDictionary<Type, IPocoClassInfo> ByType => ImmutableDictionary<Type, IPocoClassInfo>.Empty;
        }
        static IPocoClassSupportResult PocoClass = new PocoClassEmpty();

        EmptyPocoSupportResult() {}

        IReadOnlyList<IPocoRootInfo> IPocoSupportResult.Roots => Array.Empty<IPocoRootInfo>();

        IReadOnlyDictionary<Type, IPocoInterfaceInfo> IPocoSupportResult.AllInterfaces => ImmutableDictionary<Type, IPocoInterfaceInfo>.Empty;

        IReadOnlyDictionary<string, IPocoRootInfo> IPocoSupportResult.NamedRoots => ImmutableDictionary<string, IPocoRootInfo>.Empty;

        IReadOnlyDictionary<Type, IReadOnlyList<IPocoRootInfo>> IPocoSupportResult.OtherInterfaces => ImmutableDictionary<Type, IReadOnlyList<IPocoRootInfo>>.Empty;

        IPocoInterfaceInfo? IPocoSupportResult.Find( Type pocoInterface ) => null;

        bool IPocoSupportResult.IsAssignableFrom( IPocoPropertyInfo target, IPocoPropertyInfo from ) => false;

        bool IPocoSupportResult.IsAssignableFrom( IPocoPropertyInfo target, Type from, NullabilityTypeKind fromNullability ) => false;

        bool IPocoSupportResult.IsAssignableFrom( Type target, NullabilityTypeKind targetNullability, Type from, NullabilityTypeKind fromNullability ) => false;

        IPocoClassSupportResult IPocoSupportResult.PocoClass => PocoClass;
    }
}
