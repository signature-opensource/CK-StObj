using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Setup
{
    class PocoClassResult : IPocoClassSupportResult
    {
        public readonly Dictionary<Type, IPocoClassInfo> ByType;

        public PocoClassResult()
        {
            ByType = new Dictionary<Type, IPocoClassInfo>();
        }

        IReadOnlyDictionary<Type, IPocoClassInfo> IPocoClassSupportResult.ByType => ByType;
    }
}
