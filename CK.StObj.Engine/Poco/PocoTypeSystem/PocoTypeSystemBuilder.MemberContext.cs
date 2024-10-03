using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CK.Setup;


public sealed partial class PocoTypeSystemBuilder
{
    sealed class MemberContext
    {
        readonly bool _isPocoField;
        readonly IExtMemberInfo _root;
        IList<string?>? _tupleNames;
        int _tupleIndex;
        bool _forbidAbstractCollections;
        bool _forbidConcreteCollections;

        // Ctor for Poco field.
        public MemberContext( IExtMemberInfo root, bool isPocoField )
        {
            Throw.DebugAssert( isPocoField );
            _isPocoField = true;
            _root = root;
        }

        public MemberContext( IExtMemberInfo root )
        {
            _root = root;
        }

        public IExtMemberInfo Root => _root;

        public void Reset()
        {
            _forbidConcreteCollections = false;
            _forbidAbstractCollections = false;
            _tupleNames = null;
            _tupleIndex = 0;
        }

        public bool EnterListSetOrDictionary( IActivityMonitor monitor, IExtNullabilityInfo nType, bool isConcrete, string concreteType )
        {
            if( !CheckForbidden( monitor, nType, isConcrete, concreteType ) )
            {
                return false;
            }
            // Never allow an abstract collection in a collection.
            _forbidAbstractCollections = true;
            if( _isPocoField )
            {
                // Poco fields cannot have recursive collections.
                _forbidConcreteCollections = true;
            }
            return true;
        }

        public bool EnterArray( IActivityMonitor monitor, IExtNullabilityInfo nType )
        {
            // Always allow arrays of arrays.
            return _root.Type.IsArray || CheckForbidden( monitor, nType, true, "" );
        }

        bool CheckForbidden( IActivityMonitor monitor, IExtNullabilityInfo nType, bool isConcrete, string concreteType )
        {
            if( _forbidConcreteCollections && _forbidAbstractCollections )
            {
                monitor.Error( $"Invalid collection '{nType.Type:C}' in {ToString()}." );
                return false;
            }
            else if( _forbidConcreteCollections )
            {
                if( isConcrete )
                {
                    monitor.Error( $"Invalid concrete collection '{nType.Type:C}' in {ToString()}. Only IList<>, ISet<> and IDictionary<,> must be used for Poco fields." );
                    return false;
                }
            }
            else if( _forbidAbstractCollections )
            {
                if( !isConcrete )
                {
                    monitor.Error( $"Invalid abstract collection '{nType.Type:C}' in {ToString()}. It must be a {concreteType}." );
                    return false;
                }
            }
            return true;
        }

        public RecordAnonField[] EnterValueTuple( int count, out int state )
        {
            state = _forbidConcreteCollections ? 1 : 0;
            state |= _forbidAbstractCollections ? 2 : 0;
            _forbidAbstractCollections = true;
            _forbidConcreteCollections = false;
            _tupleNames ??= _root.GetCustomAttributes<TupleElementNamesAttribute>().FirstOrDefault()?.TransformNames ?? Array.Empty<string>();
            var fields = new RecordAnonField[count];
            for( int i = 0; i < fields.Length; ++i )
            {
                fields[i] = new RecordAnonField( i, _tupleNames.Count > _tupleIndex ? _tupleNames[_tupleIndex++] : null );
            }
            return fields;
        }

        public void LeaveValueTuple( int state )
        {
            _forbidConcreteCollections = (state & 1) != 0;
            _forbidAbstractCollections = (state & 2) != 0;
        }

        public override string ToString() => _root.ToString()!;

    }

}
