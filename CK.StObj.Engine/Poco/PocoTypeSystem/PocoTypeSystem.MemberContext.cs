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
            readonly IExtMemberInfo _root;
            IList<string?>? _tupleNames;
            int _tupleIndex;
            bool _forbidAbstractCollections;

            public MemberContext( IExtMemberInfo root, bool forbidAbstractCollections )
            {
                _root = root;
                _forbidAbstractCollections = forbidAbstractCollections;
                _tupleIndex = 0;
            }

            public void Reset()
            {
                _tupleIndex = 0;
                _tupleNames = null;
            }

            public bool ForbidAbstractCollections
            {
                get => _forbidAbstractCollections;
                set => _forbidAbstractCollections = value;
            }
           
            public RecordAnonField[] GetTupleNamedFields( int count )
            {
                _tupleNames ??= _root.GetCustomAttributes<TupleElementNamesAttribute>().FirstOrDefault()?.TransformNames ?? Array.Empty<string>();
                var fields = new RecordAnonField[count];
                for( int i = 0; i < fields.Length; ++i )
                {
                    fields[i] = new RecordAnonField( i, _tupleNames.Count > _tupleIndex ? _tupleNames[_tupleIndex++] : null );
                }
                return fields;
            }

            public override string ToString() => _root.ToString()!;

        }

    }

}
