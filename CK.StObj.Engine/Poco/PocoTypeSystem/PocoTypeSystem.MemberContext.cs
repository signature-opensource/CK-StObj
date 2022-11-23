using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

using NullabilityInfo = System.Reflection.TEMPNullabilityInfo;

namespace CK.Setup
{

    public sealed partial class PocoTypeSystem
    {
        sealed class MemberContext
        {
            IList<string?>? _tupleNames;
            IExtMemberInfo _root;
            int _tupleIndex;

            public MemberContext( IExtMemberInfo root )
            {
                _root = root;
                _tupleIndex = 0;
            }

            public RecordField[] GetTupleNamedFields( int count )
            {
                _tupleNames ??= _root.GetCustomAttributes<TupleElementNamesAttribute>().FirstOrDefault()?.TransformNames ?? Array.Empty<string>();
                var fields = new RecordField[count];
                for( int i = 0; i < fields.Length; ++i )
                {
                    fields[i] = new RecordField( i, _tupleNames.Count > _tupleIndex ? _tupleNames[_tupleIndex++] : null );
                }
                return fields;
            }

            public override string ToString() => _root.ToString()!;

        }

    }

}
