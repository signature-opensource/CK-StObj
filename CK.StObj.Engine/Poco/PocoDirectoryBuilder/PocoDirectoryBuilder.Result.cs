using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

#nullable enable

namespace CK.Setup
{
    partial class PocoDirectoryBuilder
    {
        sealed class Result : IPocoDirectory
        {
            public readonly List<PocoRootInfo> Roots;
            public readonly Dictionary<string, IPocoFamilyInfo> NamedRoots;
            public readonly Dictionary<Type, InterfaceInfo> AllInterfaces;
            public readonly Dictionary<Type, IReadOnlyList<IPocoFamilyInfo>> OtherInterfaces;

            readonly IReadOnlyDictionary<Type, IPocoInterfaceInfo> _exportedAllInterfaces;

            public Result()
            {
                Roots = new List<PocoRootInfo>();
                AllInterfaces = new Dictionary<Type, InterfaceInfo>();
                _exportedAllInterfaces = AllInterfaces.AsIReadOnlyDictionary<Type, InterfaceInfo, IPocoInterfaceInfo>();
                OtherInterfaces = new Dictionary<Type, IReadOnlyList<IPocoFamilyInfo>>();
                NamedRoots = new Dictionary<string, IPocoFamilyInfo>();
            }

            IReadOnlyList<IPocoFamilyInfo> IPocoDirectory.Families => Roots;

            IReadOnlyDictionary<string, IPocoFamilyInfo> IPocoDirectory.NamedFamilies => NamedRoots;

            IPocoInterfaceInfo? IPocoDirectory.Find( Type pocoInterface ) => AllInterfaces.GetValueOrDefault( pocoInterface );

            IReadOnlyDictionary<Type, IPocoInterfaceInfo> IPocoDirectory.AllInterfaces => _exportedAllInterfaces;

            IReadOnlyDictionary<Type, IReadOnlyList<IPocoFamilyInfo>> IPocoDirectory.OtherInterfaces => OtherInterfaces;

            public bool BuildNameIndex( IActivityMonitor monitor )
            {
                bool success = true;
                foreach( var r in Roots )
                {
                    var extNames = r.ExternalName;
                    if( extNames == null ) continue;
                    foreach( var name in extNames.PreviousNames.Append( r.Name ) )
                    {
                        if( NamedRoots.TryGetValue( name, out var exists ) )
                        {
                            monitor.Error( $"The Poco name '{name}' clashes: both '{r.Interfaces[0].PocoInterface:N}' and '{exists.Interfaces[0].PocoInterface:N}' share it." );
                            success = false;
                        }
                        else NamedRoots.Add( name, r );
                    }
                }
                return success;
            }

        }
    }
}
