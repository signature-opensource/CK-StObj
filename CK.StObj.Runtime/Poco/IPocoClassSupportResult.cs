using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Setup
{

    public interface IPocoClassSupportResult
    {
        IReadOnlyDictionary<Type,IPocoClassInfo> ByType { get; }
    }
}
