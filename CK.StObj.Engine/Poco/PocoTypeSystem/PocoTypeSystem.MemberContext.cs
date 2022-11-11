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
            readonly IList<string?>? _tupleNames;
            IExtMemberInfo _root;
            int _tupleIndex;
            bool? _readOnlyStatus;

            public MemberContext( IExtMemberInfo root, bool? readOnlyStatus = null )
            {
                _root = root;
                _readOnlyStatus = readOnlyStatus;
                _tupleIndex = 0;
                _tupleNames = root.GetCustomAttributes<TupleElementNamesAttribute>().FirstOrDefault()?.TransformNames ?? Array.Empty<string>();
            }

            /// <summary>
            /// Poco compliant types are either fully mutable or fully read only.
            /// </summary>
            public bool? ReadOnlyStatus => _readOnlyStatus;

            public bool CheckReadOnlyStatus( IActivityMonitor monitor, bool isReadOnly, Type t )
            {
                if( _readOnlyStatus.HasValue )
                {
                    if( _readOnlyStatus.Value != isReadOnly )
                    {
                        if( isReadOnly )
                        {
                            monitor.Error( $"{ToString()}: Invalid readonly '{t.ToCSharpName()}'. Only mutable types are allowed here." );
                        }
                        else 
                        {
                            monitor.Error( $"{ToString()}: Invalid mutable '{t.ToCSharpName()}'. Only read only types are allowed here." );
                        }
                        return false;
                    }
                    return true;
                }
                _readOnlyStatus = isReadOnly;
                return true;
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

            public override string ToString() => _root.ToString()!;

        }

    }

}
