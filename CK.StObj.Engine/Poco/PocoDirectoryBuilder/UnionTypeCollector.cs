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

namespace CK.Setup
{
    /// <summary>
    /// Encapsulates a list of PropertyInfo that must be ValueTuples. Each of them defines
    /// the possible types of the union type. No check is done at this level except the fact
    /// that all [UnionType] attribute must CanBeExtended or not, it is the PocoTypeSystem
    /// that checks the types and nullabilities.
    /// </summary>
    sealed class UnionTypeCollector
    {
        readonly List<PropertyInfo> _types;

        public UnionTypeCollector( bool canBeExtended, PropertyInfo firstDef )
        {
            _types = new List<PropertyInfo> { firstDef };
            CanBeExtended = canBeExtended;
        }

        public List<PropertyInfo> Types => _types;

        public bool CanBeExtended { get; }

        public override string ToString()
        {
            return _types.Select( t => t.ToString() ).Concatenate();
        }

    }

}
