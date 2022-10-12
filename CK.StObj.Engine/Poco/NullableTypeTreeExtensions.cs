using CK.CodeGen;
using System.Collections.Generic;

namespace CK.Setup
{
    static class NullableTypeTreeExtensions
    {
        public static IEnumerable<NullableTypeTree> GetAllSubTypes( this NullableTypeTree t, bool withThis )
        {
            if( withThis ) yield return t;
            foreach( var subType in t.SubTypes )
            {
                foreach( var s in GetAllSubTypes( subType, true ) )
                {
                    yield return s;
                }
            }
        }
    }
}
