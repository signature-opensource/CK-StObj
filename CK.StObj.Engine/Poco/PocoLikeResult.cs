using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Setup
{
    class PocoLikeResult : IPocoLikeSupportResult
    {
        public readonly Dictionary<Type, IPocoLikeInfo> ByType;

        public PocoLikeResult()
        {
            ByType = new Dictionary<Type, IPocoLikeInfo>();
        }

        IReadOnlyDictionary<Type, IPocoLikeInfo> IPocoLikeSupportResult.ByType => ByType;
    }
}
