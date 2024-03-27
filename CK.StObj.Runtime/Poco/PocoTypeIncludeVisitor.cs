using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CK.Setup
{
    /// <summary>
    /// Extends <see cref="PocoTypeVisitor"/> to visit the closure of a set of types that are typically <see cref="IPrimaryPocoType"/>.
    /// <list type="bullet">
    ///     <item>
    ///         Nullable types visit their <see cref="IPocoType.NonNullable"/> (same as base <see cref="PocoTypeVisitor"/>).
    ///     </item>
    ///     <item>
    ///         All <see cref="IPocoType.ObliviousType"/> and <see cref="IPocoType.RegularType"/> are visited.
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
    ///         An implemented <see cref="IAbstractPocoType"/> visits its <see cref="IAbstractPocoType.GenericArguments"/>, its <see cref="IAbstractPocoType.Generalizations"/>
    ///         the IPoco root interface type and <see cref="IAbstractPocoType.Fields"/>' types. This is useful to discover the AbstractReadOnly collections
    ///         (IReadOnly List/Set/Dictionary) that are referenced by a abstract Poco (other fields types will be discovered through the primaries anyway).
    ///          Note that fields that have no "real implementations" (all their implementation frileds are <see cref="PocoFieldAccessKind.AbstractReadOnly"/>)
    ///          are skipped by default (unless "withAbstractReadOnlyFieldTypes" constructor parameter has been specified).
    ///         <para>
    ///         Implementation less abstract Poco visits nothing.
    ///         </para>
    ///         <para>
    ///         If the abstract poco is the visited root, then its <see cref="IAbstractPocoType.PrimaryPocoTypes"/> are also visited: explicit include of an
    ///         abstract Poco includes all its implementations but implicit include doesn't.
    ///         </para>
    ///     </item>
    ///     <item>
    ///         Collections visit their <see cref="ICollectionPocoType.ItemTypes"/>.
    ///     </item>
    ///     <item>
    ///         <see cref="IUnionPocoType"/> visits its <see cref="IOneOfPocoType.AllowedTypes"/> (same as base <see cref="PocoTypeVisitor"/>).
    ///     </item>
    ///     <item>
    ///         <see cref="IEnumPocoType"/> visits its <see cref="IEnumPocoType.UnderlyingType"/>.
    ///     </item>
    ///     <item>
    ///         Basic types (<see cref="PocoTypeKind.Basic"/> and <see cref="PocoTypeKind.Any"/>) visit nothing else
    ///         except for <see cref="IBasicRefPocoType"/> where the <see cref="IBasicRefPocoType.BaseType"/> is visited if there's one
    ///         (including a bas type doesn't include its <see cref="IBasicRefPocoType.Specializations"/>).
    ///     </item>
    /// </list>
    /// By default this visitor visits the <see cref="ICollectionPocoType"/> that CAN be visited because their <see cref="ICollectionPocoType.ItemTypes"/>
    /// have been visited. This applies to all types (including <see cref="PocoTypeKind.Any"/>).
    /// </summary>
    public class PocoTypeIncludeVisitor<T> : PocoTypeVisitor<T> where T : class, IMinimalPocoTypeSet
    {
        readonly IPocoTypeSystem _typeSystem;
        readonly bool _visitVisitableCollections;
        readonly bool _withAbstractReadOnlyFieldTypes;
        IPocoType? _visitedRoot;
        // Cached reference to the IPoco type.
        IPocoType? _iPoco;

        /// <summary>
        /// Initializes a new <see cref="PocoTypeIncludeVisitor"/>.
        /// </summary>
        /// <param name="typeSystem">The type system that must define the visited types.</param>
        /// <param name="alreadyVisited">Already visited set of types.</param>
        /// <param name="visitVisitableCollections">False to not expand the visit to collections of items that have been visited.</param>
        /// <param name="withAbstractReadOnlyFieldTypes">
        /// True to consider the <see cref="IPrimaryPocoType"/> fields where <see cref="IPrimaryPocoField.FieldAccess"/>
        /// is <see cref="PocoFieldAccessKind.AbstractReadOnly"/>.
        /// <para>
        /// By default they are skipped. There are very few scenario where including these fields' type makes sense.
        /// </para>
        /// </param>
        public PocoTypeIncludeVisitor( IPocoTypeSystem typeSystem,
                                       T alreadyVisited,
                                       bool visitVisitableCollections = true,
                                       bool withAbstractReadOnlyFieldTypes = false )
            : base( alreadyVisited )
        {
            Throw.CheckNotNullArgument( typeSystem );
            _typeSystem = typeSystem;
            _visitVisitableCollections = visitVisitableCollections;
            _withAbstractReadOnlyFieldTypes = withAbstractReadOnlyFieldTypes;
        }

        // If we need it, it exists in the TypeSystem.
        IPocoType Poco => _iPoco ??= _typeSystem.FindByType( typeof( IPoco ) )!;

        protected override void OnStartVisit( IPocoType root )
        {
            _visitedRoot = root;
        }

        /// <summary>
        /// Overridden to visit the <see cref="IPocoType.ObliviousType"/>, <see cref="IPocoType.RegularType"/> and
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
            var r = t.RegularType;
            if( r != null && r != t && r != t.ObliviousType )
            {
                Visit( r );
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
            if( primary.AbstractTypes.Count == 0 )
            {
                Visit( Poco );
            }
            else
            {
                foreach( var a in primary.AbstractTypes )
                {
                    Visit( a );
                }
            }
            foreach( var s in primary.SecondaryTypes )
            {
                Visit( s );
            }
        }

        /// <summary>
        /// An implemented abstract Poco visits the <see cref="IAbstractPocoType.GenericArguments"/>, <see cref="IAbstractPocoType.Generalizations"/>
        /// the IPoco root interface type but also the <see cref="IAbstractPocoType.Fields"/>' types: this is specially useful to discover any
        /// <see cref="ICollectionPocoType.IsAbstractReadOnly"/> collections that cannot be "invented" by visiting the primaries poco fields.
        /// <para>
        /// Implementation less abstract Poco visits nothing.
        /// </para>
        /// <para>
        /// If the abstract poco is the visited root, then its <see cref="IAbstractPocoType.PrimaryPocoTypes"/> are also visited.
        /// </para>
        /// </summary>
        /// <param name="abstractPoco">The abstract poco.</param>
        protected override void VisitAbstractPoco( IAbstractPocoType abstractPoco )
        {
            if( abstractPoco.PrimaryPocoTypes.Count == 0 ) return;
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
            // Visiting the fields' types. This is useful to discover the AbstractReadOnly collections
            // (IReadOnly List/Set/Dictionary) that are referenced by a abstract Poco (other fields types
            // will be discovered through the primaries).
            // Note that fields that have no "real implementations" (all their implementation frileds are PocoFieldAccessKind.AbstractReadOnly)
            // are skipped by default (unless "withAbstractReadOnlyFieldTypes" parameter has been specified).
            foreach( IAbstractPocoField f in abstractPoco.Fields )
            {
                if( _withAbstractReadOnlyFieldTypes || f.Implementations.Any( impl => impl.FieldAccess != PocoFieldAccessKind.AbstractReadOnly ) )
                {
                    Visit( f.Type );
                }
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

        /// <summary>
        /// If the basic is a <see cref="IBasicRefPocoType"/>, its <see cref="IBasicRefPocoType.BaseType"/> is visited.
        /// </summary>
        /// <param name="basic">The basic type.</param>
        protected override void VisitBasic( IPocoType basic )
        {
            if( basic is IBasicRefPocoType b
                && b.BaseType != null )
            {
                Visit( b.BaseType );
            }
        }

    }

}
