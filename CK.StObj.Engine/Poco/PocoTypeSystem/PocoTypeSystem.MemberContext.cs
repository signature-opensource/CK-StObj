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
            readonly bool _isProperType;
            readonly IExtMemberInfo _root;
            IList<string?>? _tupleNames;
            int _tupleIndex;
            MemberContext? _parent;
            bool _forbidAbstractCollections;
            bool _forbidCollections;

            public MemberContext( bool isProperType, IExtMemberInfo root )
            {
                _isProperType = isProperType;
                // Compliant types don't have abstract collections.
                _forbidAbstractCollections = !isProperType;
                _root = root;
                _tupleIndex = 0;
            }

            public MemberContext( MemberContext parent, IExtMemberInfo root )
                : this( parent._isProperType, root )
            {
                _parent = parent;
            }

            public void Reset( bool forbidCollections, bool forbidAbstractCollections )
            {
                _tupleIndex = 0;
                _tupleNames = null;
                _forbidCollections = forbidCollections;
                _forbidAbstractCollections = forbidAbstractCollections && !_isProperType;
            }

            public bool IsProperType => _isProperType;

            public bool ForbidAbstractCollections
            {
                get => _forbidAbstractCollections;
                set => _forbidAbstractCollections = value;
            }

            public bool ForbidCollections
            {
                get => _forbidCollections;
                set => _forbidCollections = value;
            }


            public bool EnterListSetOrDictionary( IActivityMonitor monitor, IExtNullabilityInfo nType, bool isRegular, string concreteType )
            {
                if( !CheckForbidden( monitor, nType, isRegular, concreteType ) )
                {
                    return false;
                }
                if( _isProperType )
                {
                    _forbidCollections = true;
                }
                else
                {
                    // Never allow an abstract collection in a collection.
                    _forbidAbstractCollections = true;
                }
                return true;
            }

            public bool EnterArray( IActivityMonitor monitor, IExtNullabilityInfo nType )
            {
                // Always allow arrays of arrays. Arrays are not proper types.
                return _root.Type.IsArray || CheckForbidden( monitor, nType, true, "" );
            }

            bool CheckForbidden( IActivityMonitor monitor, IExtNullabilityInfo nType, bool isRegular, string concreteType )
            {
                if( _forbidCollections )
                {
                    monitor.Error( $"Invalid collection '{nType.Type:C}' in {ToString()}." );
                    return false;
                }
                else if( _forbidAbstractCollections )
                {
                    if( !isRegular )
                    {
                        monitor.Error( $"Invalid abstract collection '{nType.Type:C}' in {ToString()}. It must be a {concreteType}." );
                        return false;
                    }
                }
                return true;
            }

            internal static bool IsValidProperType( IRecordPocoField field )
            {
                var invalid = field.Type.Kind is PocoTypeKind.AbstractPoco or PocoTypeKind.SecondaryPoco or PocoTypeKind.PrimaryPoco
                                              or PocoTypeKind.List or PocoTypeKind.HashSet or PocoTypeKind.Dictionary or PocoTypeKind.Array;
                return !invalid && field.Type.IsProperType;
            }

            internal bool CheckRecordProperType( IActivityMonitor monitor, IRecordPocoType recordPocoType )
            {
                if( _parent == null && _isProperType && !recordPocoType.IsProperType )
                {
                    var invalid = recordPocoType.Fields.Where( f => !IsValidProperType( f ) );
                    if( invalid.Any() )
                    {
                        var fields = invalid.Select( f => $"{Environment.NewLine}{f.Type.CSharpName} {f.Name}" );
                        monitor.Error( $"Invalid mutable reference types in '{recordPocoType.CSharpName:N}':{fields.Concatenate()}" );
                        return false;
                    }
                }
                return true;
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
