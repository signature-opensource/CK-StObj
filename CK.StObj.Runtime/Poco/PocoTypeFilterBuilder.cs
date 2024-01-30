using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace CK.Setup
{
    public sealed class PocoTypeFilterBuilder
    {
        readonly Dictionary<IPocoType, (bool WithImplementationLessAbstract, bool WithSecondaryPoco, bool WithEmptyNamedRecord)> _inclTypes;
        readonly HashSet<IPocoType> _exclTypes;
        readonly Dictionary<PocoTypeFilter, bool> _filters;
        readonly IPocoTypeSystem _typeSystem;
        readonly bool _allowAll;
        PocoTypeFilter? _result;

        public PocoTypeFilterBuilder( IPocoTypeSystem typeSystem, bool allowAll = true )
        {
            _typeSystem = typeSystem;
            _allowAll = allowAll;
            _inclTypes = new Dictionary<IPocoType, (bool, bool, bool)>();
            _exclTypes = new HashSet<IPocoType>();
            _filters = new Dictionary<PocoTypeFilter, bool>();
        }

        public bool IsLocked => _result != null;

        public PocoTypeFilter Lock() => _result ??= Build();

        /// <summary>
        /// Adds a low priority "Or" filter.
        /// </summary>
        /// <param name="filter">The filter to add.</param>
        public void AddOr( PocoTypeFilter filter )
        {
            if( !_allowAll && filter != PocoTypeFilter.AllowNone )
            {
                _filters[filter] = true;
            }
        }

        /// <summary>
        /// Adds a "And" filter: such filters are applied after the "Or" filters.
        /// </summary>
        /// <param name="filter">The filter to add.</param>
        public void AddAnd( PocoTypeFilter filter )
        {
            if( filter != PocoTypeFilter.AllowAll )
            {
                _filters[filter] = false;
            }
        }

        /// <summary>
        /// Removes a filter added with <see cref="AddOr(PocoTypeFilter)"/> or <see cref="AddAnd(PocoTypeFilter)"/>.
        /// </summary>
        /// <param name="filter">The filter to remove.</param>
        public void Remove( PocoTypeFilter filter )
        {
            _filters.Remove( filter );
        }

        /// <summary>
        /// Explicitly allows one type.
        /// </summary>
        /// <param name="t">The type to allow.</param>
        /// <param name="withImplementationLessAbstract">True to allow Abstact Poco without Primary Poco implementation.</param>
        /// <param name="withSecondaryPoco">True to allow secondary poco interfaces.</param>
        /// <param name="withEmptyNamedRecord">True to allow empty named record (struct without fields).</param>
        public void Allow( IPocoType t, bool withImplementationLessAbstract = false, bool withSecondaryPoco = false, bool withEmptyNamedRecord = false )
        {
            Throw.CheckNotNullArgument( t );
            _inclTypes[t] = (withImplementationLessAbstract, withSecondaryPoco, withEmptyNamedRecord);
            _exclTypes.Remove( t );
        }

        /// <summary>
        /// Explicitly disallow one type.
        /// <see cref="PocoTypeKind.Any"/> throws an <see cref="ArgumentException"/>.
        /// </summary>
        /// <param name="t">The type to disallow.</param>
        public void Disallow( IPocoType t )
        {
            Throw.CheckNotNullArgument( t );
            Throw.CheckArgument( t.Kind != PocoTypeKind.Any );
            _exclTypes.Add( t );
            _inclTypes.Remove( t );
        }

        /// <summary>
        /// Removes a type that has been added configured by <see cref="Allow(IPocoType, bool, bool)"/>
        /// or <see cref="Disallow(IPocoType)"/>.
        /// </summary>
        /// <param name="t">The type to disallow.</param>
        public void Remove( IPocoType t )
        {
            Throw.CheckNotNullArgument( t );
            _inclTypes.Remove( t );
            _exclTypes.Remove( t );
        }

        sealed class ActualFiler : PocoTypeFilter
        {
            internal readonly IPocoTypeSystem _typeSystem;
            internal readonly BitArray _flags;

            public ActualFiler( IPocoTypeSystem typeSystem, BitArray flags )
            {
                _typeSystem = typeSystem;
                _flags = flags;
            }

            public override bool IsAllowed( IPocoType t ) => _flags[t.Index >> 1];
        }


        sealed class Allower : PocoTypeIncludeVisitor
        {
            readonly BitArray _flags;
            readonly bool _withEmptyNamedRecord;
            bool _visitResult;

            public Allower( BitArray flags, bool withImplementationLessAbstractPoco, bool withSecondaryPoco, bool withEmptyNamedRecord )
                : base( withImplementationLessAbstractPoco, withSecondaryPoco ) 
            {
                _flags = flags;
                _withEmptyNamedRecord = withEmptyNamedRecord;
            }

            protected override bool Visit( IPocoType t )
            {
                // Don't do this:
                // if( _flags[t.Index >> 1] ) return true;
                // For composites (record and Poco) we can have disallowd fields's type even
                // if the type itself is allowed.
                // This include visitor must "fully allow" a type!
                _visitResult = true;
                if( base.Visit( t ) )
                {
                    _flags[t.Index >> 1] = _visitResult;
                    return true;
                }
                return false;
            }

            protected override void VisitRecord( IRecordPocoType record )
            {
                if( !_withEmptyNamedRecord && !record.IsAnonymous && record.Fields.Count == 0 )
                {
                    _visitResult = false;
                }
                else base.VisitRecord( record );
            }

            protected override void VisitSecondaryPoco( ISecondaryPocoType secondary )
            {
                base.VisitSecondaryPoco( secondary );
                _visitResult = VisitSecondaryPocoTypes;
            }

            protected override void VisitAbstractPoco( IAbstractPocoType abstractPoco )
            {
                base.VisitAbstractPoco( abstractPoco );
                _visitResult = VisitImplementationLessAbstractPoco;
            }
        }

        sealed class Disallower
        {
            readonly BitArray _flags;
            readonly HashSet<IPocoType> _processed;
            HashSet<IUnionPocoType>? _unionTypeCheck;
            HashSet<ICompositePocoType>? _compositeTypeCheck;

            public Disallower( BitArray flags )
            {
                _flags = flags;
                _processed = new HashSet<IPocoType>();
            }

            public void Disallow( IPocoType t )
            {
                // Avoids any reentrancy but based on our processed set,
                // not on the _flags.
                if( _processed.Add( t ) )
                {
                    _flags[t.Index >> 1] = false;

                    if( t is IAbstractPocoType abs )
                    {
                        // Disallow all its specializations, including the primaries.
                        foreach( var s in abs.AllowedTypes )
                        {
                            Disallow( s );
                        }
                    }
                    else if( t is IPrimaryPocoType primary )
                    {

                    }
                    var backRef = t.FirstBackReference;
                    while( backRef != null )
                    {
                        if( backRef.Owner.Type is IUnionPocoType oneOf )
                        {
                            _unionTypeCheck ??= new HashSet<IUnionPocoType>();
                            _unionTypeCheck.Add( oneOf );
                        }
                        else if( backRef.Owner.Type is ICompositePocoType composite )
                        {
                            _compositeTypeCheck ??= new HashSet<ICompositePocoType>();
                            _compositeTypeCheck.Add( composite );
                        }
                        else
                        {
                            Throw.DebugAssert( "We are left with IAbstractPocoType and ICollectionType (generic arguments)", backRef.Owner.Type is ICollectionPocoType or IAbstractPocoType );
                            Disallow( backRef.Owner );
                        }
                        backRef = backRef.NextRef;
                    }
                }
            }

            void OnDisallowed( IAbstractPocoType a, IPocoType.ITypeRef backRef )
            {
                // If the disallowed type is a generic parameter type, this disallows
                // the whole abstract IPoco that disables all its specializations, including the primaries.
                if( backRef.Index < 0 )
                {
                    Throw.DebugAssert( ~backRef.Index < a.GenericArguments.Count );
                    Disallow( a );
                    return;
                }
            }

            void OnNoMoreExchangeable( IReadOnlyList<IPocoField> fields, IPocoType.ITypeRef r )
            {
                int exCount = 0;
                foreach( var f in fields )
                {
                    if( _flags[f.Type.Index>>1] ) exCount++;
                    else if( f.Type == r.Type )
                    {
                        monitor.Info( $"Field '{composite}.{f.Name}' is no more exchangeable because its type '{f.Type}' is no more exchangeable." );
                    }
                }
                if( exCount == 0 )
                {
                    composite.SetNotExchangeable( monitor, $"no more fields are exchangeable." );
                }
            }

        }

        PocoTypeFilter Build()
        {
            // Half-size bit flags: only non nullables need to be handled.
            BitArray? flags = new BitArray( _typeSystem.AllNonNullableTypes.Count, _allowAll );
            // Applies the "Or" flags first if needed.
            if( !_allowAll )
            {
                foreach( var (f, allow) in _filters )
                {
                    if( allow )
                    {
                        if( f == PocoTypeFilter.AllowAll )
                        {
                            flags.SetAll( true );
                            break;
                        }
                        Throw.DebugAssert( f != PocoTypeFilter.AllowNone && f is ActualFiler );
                        flags.Or( Unsafe.As<ActualFiler>( f )._flags );
                    }
                }
            }
            // Applies the "And" flags.
            foreach( var (f, allow) in _filters )
            {
                if( !allow )
                {
                    if( f == PocoTypeFilter.AllowNone )
                    {
                        flags.SetAll( false );
                        break;
                    }
                    Throw.DebugAssert( f != PocoTypeFilter.AllowAll && f is ActualFiler );
                    flags.And( Unsafe.As<ActualFiler>( f )._flags );
                }
            }
            // Applies the "Allowed" types.
            Allower? allower = null;
            foreach( var (t, config) in _inclTypes )
            {
                allower ??= new Allower( flags, config.WithImplementationLessAbstract, config.WithSecondaryPoco );
                allower.VisitRoot( t );
            }
            // Applies the "Disallowed" types.
            Disallower? disallower = null;
            foreach( var t in _exclTypes )
            {
                disallower ??= new Disallower( flags );
                disallower.Disallow( t );
            }

        }

    }

}
