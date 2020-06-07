using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection.Emit;

#nullable enable

namespace CK.Setup
{
    partial class PocoRegisterer
    {
        class Result : IPocoSupportResult
        {
            readonly IReadOnlyDictionary<Type, IPocoInterfaceInfo> _exportedInterfaces;
            public readonly List<ClassInfo> Roots;
            public readonly Dictionary<Type, InterfaceInfo> AllInterfaces;
            public readonly Dictionary<Type, IReadOnlyList<IPocoRootInfo>> OtherInterfaces;
            public Type? FinalFactory;

            // Exposed FinalFactory is necessarily not null.
            Type IPocoSupportResult.FinalFactory => FinalFactory!;

            public Result()
            {
                Roots = new List<ClassInfo>();
                AllInterfaces = new Dictionary<Type, InterfaceInfo>();
                _exportedInterfaces = AllInterfaces.AsCovariantReadOnly<Type, InterfaceInfo, IPocoInterfaceInfo>();
                OtherInterfaces = new Dictionary<Type, IReadOnlyList<IPocoRootInfo>>();
            }

            IReadOnlyList<IPocoRootInfo> IPocoSupportResult.Roots => Roots;

            IPocoInterfaceInfo? IPocoSupportResult.Find( Type pocoInterface ) => AllInterfaces.GetValueOrDefault( pocoInterface );

            IReadOnlyDictionary<Type, IPocoInterfaceInfo> IPocoSupportResult.AllInterfaces => _exportedInterfaces;

            IReadOnlyDictionary<Type, IReadOnlyList<IPocoRootInfo>> IPocoSupportResult.OtherInterfaces => OtherInterfaces;
        }

        class ClassInfo : IPocoRootInfo
        {
            public Type PocoClass { get; }
            public Type? ClosureInterface { get; }
            public bool IsClosedPoco { get; }
            public readonly MethodBuilder StaticMethod;
            public readonly List<InterfaceInfo> Interfaces;
            public HashSet<Type> OtherInterfaces;
            IReadOnlyList<IPocoInterfaceInfo> IPocoRootInfo.Interfaces => Interfaces;
            IReadOnlyCollection<Type> IPocoRootInfo.OtherInterfaces => OtherInterfaces;

            public ClassInfo( Type pocoClass, MethodBuilder method, bool mustBeClosed, Type? closureInterface, HashSet<Type> others )
            {
                PocoClass = pocoClass;
                ClosureInterface = closureInterface;
                IsClosedPoco = mustBeClosed;
                StaticMethod = method;
                Interfaces = new List<InterfaceInfo>();
                OtherInterfaces = others;
            }
        }

        class InterfaceInfo : IPocoInterfaceInfo
        {
            public readonly ClassInfo Root;
            public Type PocoInterface { get; }
            public Type PocoFactoryInterface { get; }

            IPocoRootInfo IPocoInterfaceInfo.Root => Root;

            public InterfaceInfo( ClassInfo root, Type pocoInterface, Type pocoFactoryInterface )
            {
                Root = root;
                PocoInterface = pocoInterface;
                PocoFactoryInterface = pocoFactoryInterface;
            }
        }

    }
}
