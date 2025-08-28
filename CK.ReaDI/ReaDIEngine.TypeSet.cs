using CK.Engine.TypeCollector;
using System.Collections;
using System.Collections.Generic;

namespace CK.Core;

public sealed partial class ReaDIEngine
{
    public sealed class TypeSet : IReadOnlySet<ICachedType>
    {
        readonly Dictionary<ICachedType,SourcedType> _sourceTypes;
        readonly ReaDIEngine _engine;
        HashSet<ICachedType>? _asSet;

        internal TypeSet( ReaDIEngine engine )
        {
            _sourceTypes = new Dictionary<ICachedType, SourcedType>();
            _engine = engine;
        }

        public int Count => _sourceTypes.Count;

        public bool Contains( ICachedType item ) => _sourceTypes.ContainsKey( item );

        public bool Add( IActivityMonitor monitor, ICachedType type )
        {
            if( _sourceTypes.TryGetValue( type, out var sourcedType ) )
            {
                sourcedType._inactive = false;
                return true;
            }
            sourcedType = new SourcedType( type );
            // The type should be final, "eventually concrete", and not optional.
            // There is not yet any "optionality" at this level.
            // There is no IsEventuallyConcrete on a type for the moment.
            // The FinalTypeSet has yet to be done.
            // For the moment, only accepts classes.
            if( type.Type.IsClass && !type.IsDelegate )
            {
                if( RegisterEngineAttributesReaDIHandler( monitor, _engine, sourcedType, type ) )
                {
                    bool success = true;
                    foreach( var m in type.Members )
                    {
                        success &= RegisterEngineAttributesReaDIHandler( monitor, _engine, sourcedType, m );
                    }
                    if( !success ) return _engine.SetError( monitor );
                }
            }
            _asSet?.Add( type );
            _sourceTypes.Add( type, sourcedType );
            return true;

            static bool RegisterEngineAttributesReaDIHandler( IActivityMonitor monitor, ReaDIEngine engine, SourcedType sourcedType, ICachedItem item )
            {
                if( !item.TryGetAllAttributes( monitor, out var attributes ) )
                {
                    return false;
                }
                bool success = true;
                foreach( var a in attributes )
                {
                    if( a is IReaDIHandler handler )
                    {
                        var tHandler = engine._typeCache.Get( a.GetType() );
                        success &= engine._typeRegistrar.RegisterHandlerTypeForSourceType( monitor, engine, tHandler, sourcedType, handler );
                    }
                    else if( engine._debugMode && !CheckNoReaDIMethodsOnNonReaDIHandler( monitor, engine._typeCache.Get( a.GetType() ) ) )
                    {
                        success = false;
                    }
                }
                return success;
            }

        }


        #region IReadOnlySet<ICachedType> implementation.
        public IEnumerator<ICachedType> GetEnumerator() => _sourceTypes.Keys.GetEnumerator();

        public bool IsSupersetOf( IEnumerable<ICachedType> other )
        {
            if( other == this ) return true;
            if( other is ICollection<ICachedType> otherAsCollection )
            {
                if( otherAsCollection.Count == 0 ) return true;
                if( other is IReadOnlySet<ICachedType> otherAsSet
                    && otherAsSet.Count > _sourceTypes.Count )
                {
                    return false;
                }
            }
            foreach( var e in other )
            {
                if( !_sourceTypes.ContainsKey( e ) )  return false;
            }
            return true;
        }

        public bool Overlaps( IEnumerable<ICachedType> other )
        {
            if( _sourceTypes.Count == 0 ) return false;
            if( other == this ) return true;
            foreach( var e in other )
            {
                if( _sourceTypes.ContainsKey( e ) ) return true;
            }
            return false;
        }

        IEnumerator IEnumerable.GetEnumerator() => _sourceTypes.Keys.GetEnumerator();

        HashSet<ICachedType> EnsureSet() => _asSet ??= new HashSet<ICachedType>( _sourceTypes.Keys );

        public bool IsProperSubsetOf( IEnumerable<ICachedType> other )
        {
            if( other == this ) return false;

            if( other is ICollection<ICachedType> otherAsCollection )
            {
                if( otherAsCollection.Count <= _sourceTypes.Count ) return false;
                if( _sourceTypes.Count == 0 ) return true;
                if( other is IReadOnlySet<ICachedType> otherAsSet )
                {
                    return IsSubsetOfSet( otherAsSet );
                }
            }
            return EnsureSet().IsProperSubsetOf( other );
        }

        bool IsSubsetOfSet( IReadOnlySet<ICachedType> other )
        {
            foreach( var t in this )
            {
                if( !other.Contains( t ) ) return false;
            }
            return true;
        }

        public bool IsProperSupersetOf( IEnumerable<ICachedType> other )
        {
            if( _sourceTypes.Count == 0 || other == this ) return false;
            if( other is ICollection<ICachedType> otherAsCollection )
            {
                if( otherAsCollection.Count == 0 ) return true;
                if( other is IReadOnlySet<ICachedType> otherAsSet )
                {
                    if( otherAsSet.Count >= _sourceTypes.Count ) return false;
                    return IsSupersetOfSet( otherAsSet );
                }
            }
            return EnsureSet().IsProperSupersetOf( other );
        }

        bool IsSupersetOfSet( IReadOnlySet<ICachedType> other )
        {
            foreach( var t in other )
            {
                if( !_sourceTypes.ContainsKey( t ) ) return false;
            }
            return true;
        }

        public bool IsSubsetOf( IEnumerable<ICachedType> other )
        {
            if( _sourceTypes.Count == 0 || other == this ) return true;
            if( other is ICollection<ICachedType> otherAsCollection )
            {
                if( _sourceTypes.Count > otherAsCollection.Count ) return false;
                if( other is IReadOnlySet<ICachedType> otherAsSet )
                {
                    return IsSubsetOfSet( otherAsSet );
                }
            }
            return EnsureSet().IsSubsetOf( other );
        }

        public bool SetEquals( IEnumerable<ICachedType> other )
        {
            if( other == this ) return true;
            if( other is ICollection<ICachedType> otherAsCollection )
            {
                if( _sourceTypes.Count == 0 ) return otherAsCollection.Count == 0;
                if( other is IReadOnlySet<ICachedType> otherAsSet )
                {
                    if( _sourceTypes.Count != otherAsSet.Count ) return false;
                    return IsSubsetOfSet( otherAsSet );
                }
                if( _sourceTypes.Count > otherAsCollection.Count ) return false;
            }
            return EnsureSet().SetEquals( other );
        }

        #endregion
    }
}

