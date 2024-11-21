using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static CK.Setup.PocoType;

namespace CK.Setup;

sealed class UnionTypeCollector : IUnionTypeCollector
{
    readonly List<IExtMemberInfo> _types;

    public UnionTypeCollector( bool canBeExtended, IExtMemberInfo firstDef )
    {
        _types = new List<IExtMemberInfo> { firstDef };
        CanBeExtended = canBeExtended;
    }

    public List<IExtMemberInfo> Types => _types;

    public bool CanBeExtended { get; }

    IReadOnlyList<IExtMemberInfo> IUnionTypeCollector.Types => Types;

    public override string ToString()
    {
        return _types.Select( t => t.HomogeneousNullabilityInfo?.ToString()
                                   ?? "[ReadState]" + t.ReadNullabilityInfo.ToString() )
                     .Concatenate();
    }

}
