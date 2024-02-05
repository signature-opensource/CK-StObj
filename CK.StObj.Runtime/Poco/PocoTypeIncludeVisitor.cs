using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Extends <see cref="PocoTypeVisitor"/> to visit the closure of a set of types that are typically <see cref="IPrimaryPocoType"/>.
    /// <list type="bullet">
    ///     <item>
    ///         Nullable types visit their <see cref="IPocoType.NonNullable"/> (same as base <see cref="PocoTypeVisitor"/>).
    ///     </item>
    ///     <item>
    ///         <see cref="IRecordPocoType"/> visits its <see cref="IRecordPocoField"/> (same as base <see cref="PocoTypeVisitor"/>).
    ///     </item>
    ///     <item>
    ///         <see cref="IPrimaryPocoType"/> visits its <see cref="IPrimaryPocoField"/> and its <see cref="IPrimaryPocoType.SecondaryTypes"/>.
    ///         The <see cref="IPrimaryPocoType.AbstractTypes"/> are visited (but this doesn't trigger the visit of their primary types).
    ///         <para>
    ///         By default Poco fields that are <see cref="PocoFieldAccessKind.AbstractReadOnly"/> are ignored (because they are useless).
    ///         </para>
    ///     </item>
    ///     <item>
    ///         <see cref="ISecondaryPocoType"/> visits their <see cref="ISecondaryPocoType.PrimaryPocoType"/> (same as base <see cref="PocoTypeVisitor"/>).
    ///     </item>
    ///     <item>
    ///         <see cref="IAbstractPocoType"/> visits its <see cref="IAbstractPocoType.GenericArguments"/> and its <see cref="IAbstractPocoType.Generalizations"/>
    ///         in addition to the IPoco root interface type.
    ///         <para>
    ///         If the abstract poco is the visited root, then its <see cref="IAbstractPocoType.PrimaryPocoTypes"/> are also visited: explicit include of an
    ///         abstract Poco includes all its implementations but implicit include doesn't.
    ///         </para>
    ///     </item>
    ///     <item>
    ///         Collections visit their <see cref="ICollectionPocoType.ItemTypes"/> AbstractReadOnly ones (IReadOnlyXXX) visit their
    ///         <see cref="ICollectionPocoType.MutableCollection"/>.
    ///     </item>
    ///     <item>
    ///         <see cref="IUnionPocoType"/> visits its <see cref="IOneOfPocoType.AllowedTypes"/> (same as base <see cref="PocoTypeVisitor"/>).
    ///     </item>
    ///     <item>
    ///         <see cref="IEnumPocoType"/> visits its <see cref="IEnumPocoType.UnderlyingType"/>.
    ///     </item>
    ///     <item>
    ///         All <see cref="IPocoType.ObliviousType"/> are visited.
    ///     </item>
    ///     <item>
    ///         Basic types (<see cref="PocoTypeKind.Basic"/> and <see cref="PocoTypeKind.Any"/>) visit nothing else (same as base <see cref="PocoTypeVisitor"/>).
    ///     </item>
    /// </list>
    /// By default this visitor visits the <see cref="ICollectionPocoType"/> that CAN be visited because their <see cref="ICollectionPocoType.ItemTypes"/>
    /// have been visited. This applies to all types (including <see cref="PocoTypeKind.Any"/>).
    /// </summary>
    public class PocoTypeIncludeVisitor : PocoTypeVisitor<PocoTypeRawSet>
    {
        readonly bool _visitVisitableCollections;
        readonly bool _withAbstractReadOnlyFieldTypes;
        IPocoType? _visitedRoot;
        // Cached reference to the IPoco type.
        IPocoType? _iPoco;

        /// <summary>
        /// Initializes a new <see cref="PocoTypeIncludeVisitor"/>.
        /// </summary>
        /// <param name="alreadyVisited">Already visited set of types.</param>
        /// <param name="visitVisitableCollections">False to not expand the visit to collections of items that have been visited.</param>
        /// <param name="withAbstractReadOnlyFieldTypes">
        /// True to consider the <see cref="IPrimaryPocoType"/> fields where <see cref="IPrimaryPocoField.FieldAccess"/>
        /// is <see cref="PocoFieldAccessKind.AbstractReadOnly"/>.
        /// <para>
        /// By default they are skipped. There are very few scenario where including these fields' type makes sense.
        /// </para>
        /// </param>
        public PocoTypeIncludeVisitor( PocoTypeRawSet alreadyVisited,
                                       bool visitVisitableCollections = true,
                                       bool withAbstractReadOnlyFieldTypes = false )
            : base( alreadyVisited )
        {
            _visitVisitableCollections = visitVisitableCollections;
            _withAbstractReadOnlyFieldTypes = withAbstractReadOnlyFieldTypes;
        }

        // If we need it, it exists in the TypeSystem.
        IPocoType Poco => _iPoco ??= LastVisited.TypeSystem.FindByType( typeof( IPoco ) )!;

        protected override void OnStartVisit( IPocoType root )
        {
            _visitedRoot = root;
        }

        /// <summary>
        /// Overridden to first call the include filter predicate, visit the <see cref="IPocoType.ObliviousType"/> and
        /// visit referencing collections if needed.
        /// </summary>
        /// <param name="t">The type to visit.</param>
        /// <returns>True if the type has been visited, false if it has been skipped (already visited or excluded by the filter).</returns>
        protected override bool Visit( IPocoType t )
        {
            if( !base.Visit( t ) ) return false;
            // VisitSecondaryPoco visits its PrimaryPoco that is its Oblivious: no need to visit it again.
            if( !t.IsOblivious && t.Kind != PocoTypeKind.SecondaryPoco )
            {
                Visit( t.ObliviousType );
            }
            if( _visitVisitableCollections )
            {
                var b = t.FirstBackReference;
                while( b != null )
                {
                    if( b.Owner is ICollectionPocoType c && !LastVisited.Contains( c ) )
                    {
                        if( c.ItemTypes.Count == 1 || c.ItemTypes.All( item => item == t || LastVisited.Contains( item ) ) )
                        {
                            Visit( c );
                        }
                    }
                    b = b.NextRef;
                }
            }
            return true;
        }

        /// <summary>
        /// Visits <see cref="IPrimaryPocoType.Fields"/> and <see cref="IPrimaryPocoType.AbstractTypes"/>.
        /// </summary>
        /// <param name="primary">The primary poco type.</param>
        protected override void VisitPrimaryPoco( IPrimaryPocoType primary )
        {
            foreach( var f in primary.Fields )
            {
                if( !(_withAbstractReadOnlyFieldTypes && f.FieldAccess == PocoFieldAccessKind.AbstractReadOnly) )
                {
                    VisitField( f );
                }
            }
            foreach( var a in primary.AbstractTypes )
            {
                Visit( a );
            }
            foreach( var s in primary.SecondaryTypes )
            {
                Visit( s );
            }
        }

        /// <summary>
        /// Visits the <see cref="IAbstractPocoType.GenericArguments"/>, <see cref="IAbstractPocoType.Generalizations"/>
        /// and the root IPoco root interface type.
        /// <para>
        /// If the abstract poco is the visited root, then its <see cref="IAbstractPocoType.PrimaryPocoTypes"/> are also visited.
        /// </para>
        /// </summary>
        /// <param name="abstractPoco">The abstract poco.</param>
        protected override void VisitAbstractPoco( IAbstractPocoType abstractPoco )
        {
            Visit( Poco );
            foreach( var a in abstractPoco.GenericArguments )
            {
                Visit( a.Type );
            }
            // If we visit the primaries, it is useless to visit these Generalizations
            // (primary will do it).
            if( abstractPoco == _visitedRoot )
            {
                foreach( var p in abstractPoco.PrimaryPocoTypes )
                {
                    Visit( p );
                }
            }
            else
            {
                foreach( var a in abstractPoco.Generalizations )
                {
                    Visit( a );
                }
            }
        }

        /// <summary>
        /// Visit the <see cref="ICollectionPocoType.ItemTypes"/> and the <see cref="ICollectionPocoType.MutableCollection"/>.
        /// </summary>
        /// <param name="collection">The collection type.</param>
        protected override void VisitCollection( ICollectionPocoType collection )
        {
            if( collection.IsAbstractCollection )
            {
                Throw.DebugAssert( collection.ItemTypes == collection.MutableCollection.ItemTypes );
                Visit( collection.MutableCollection );
            }
            else
            {
                // Visit the ItemTypes.
                base.VisitCollection( collection );
            }
        }

        /// <summary>
        /// Overridden to visit the <see cref="IEnumPocoType.UnderlyingType"/>.
        /// </summary>
        /// <param name="e">The enum type.</param>
        protected override void VisitEnum( IEnumPocoType e )
        {
            Visit( e.UnderlyingType );
        }

    }

}
