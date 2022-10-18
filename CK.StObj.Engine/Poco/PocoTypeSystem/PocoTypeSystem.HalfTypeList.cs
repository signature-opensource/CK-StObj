using System.Collections;
using System.Collections.Generic;

namespace CK.Setup
{

    public sealed partial class PocoTypeSystem
    {
        sealed class HalfTypeList : IReadOnlyList<IPocoType>
        {
            readonly List<PocoType> _allTypes;

            public HalfTypeList( List<PocoType> allTypes )
            {
                _allTypes = allTypes;
            }

            public IPocoType this[int index]
            {
                get
                {
                    var t = _allTypes[index>>1];
                    if( (index & 1) == 0 ) return t;
                    return t.Nullable;
                }
            }

            public int Count => _allTypes.Count << 1;

            public IEnumerator<IPocoType> GetEnumerator()
            {
                foreach( var t in _allTypes )
                {
                    yield return t;
                    yield return t.Nullable;  
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

    }

}
