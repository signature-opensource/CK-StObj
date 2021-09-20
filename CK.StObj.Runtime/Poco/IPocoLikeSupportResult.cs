using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Setup
{

    public interface IPocoLikeSupportResult
    {
        IReadOnlyDictionary<Type,IPocoLikeInfo> ByType { get; }
    }
}
