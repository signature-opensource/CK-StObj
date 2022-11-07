using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

using NullabilityInfo = System.Reflection.TEMPNullabilityInfo;

namespace CK.Setup
{

    public sealed partial class PocoTypeSystem
    {
        sealed class MemberContext
        {
            readonly IList<string?>? _tupleNames;
            MemberInfo? _member;
            ParameterInfo? _parameter;
            int _tupleIndex;

            public MemberContext( MemberInfo root )
            {
                _member = root;
                _tupleIndex = 0;
                _tupleNames = root.GetCustomAttribute<TupleElementNamesAttribute>()?.TransformNames ?? Array.Empty<string>();
            }

            public MemberContext( ParameterInfo root )
            {
                _parameter = root;
                _tupleIndex = 0;
                _tupleNames = root.GetCustomAttribute<TupleElementNamesAttribute>()?.TransformNames ?? Array.Empty<string>();
            }

            public RecordField[] GetTupleNamedFields( int count )
            {
                var fields = new RecordField[count];
                for( int i = 0; i < fields.Length; ++i )
                {
                    fields[i] = new RecordField( i, _tupleNames?[_tupleIndex++] );
                }
                return fields;
            }

            public override string ToString()
            {
                if( _member != null )
                {
                    var type = _member is PropertyInfo ? "Property" : "Field";
                    return $"{type} '{_member.DeclaringType.ToCSharpName()}.{_member.Name}'";
                }
                Debug.Assert( _parameter != null );
                return $"Parameter '{_parameter.Name}' of method '{_parameter.Member.DeclaringType.ToCSharpName(false)}.{_parameter.Member.Name}'";
            }

        }

    }

}
