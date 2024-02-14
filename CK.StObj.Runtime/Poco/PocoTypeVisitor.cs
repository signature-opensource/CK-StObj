using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace CK.Setup
{
    /// <summary>
    /// Safe <see cref="IPocoType"/> base visitor (a type is visited only once), visited types
    /// are available in <see cref="LastVisited"/> that is a <see cref="IMinimalPocoTypeSet"/>.
    /// <para>
    /// The behavior regarding nullability depends on the <see cref="LastVisited"/> <see cref="IMinimalPocoTypeSet"/>
    /// implementation: if the set tracks nullables and non nullables differently then both will be visited (<c>int?</c> and <c>int</c>).
    /// In practice, sets handle half of the types (only the non nullables): visiting a nullable is the same as visiting its non nullable
    /// associated type.
    /// </para>
    /// <para>
    /// By default:
    /// <list type="bullet">
    ///     <item>
    ///     <see cref="IRecordPocoType"/> visits its <see cref="IRecordPocoField"/>.
    ///     </item>
    ///     <item>
    ///     <see cref="IPrimaryPocoType"/> only visits its <see cref="IPocoField"/>.
    ///     </item>
    ///     <item>
    ///     <see cref="ISecondaryPocoType"/> visits their <see cref="ISecondaryPocoType.PrimaryPocoType"/>.
    ///     </item>
    ///     <item>
    ///     <see cref="VisitAbstractPoco(IAbstractPocoType)"/> does nothing: <see cref="IAbstractPocoType.AllSpecializations"/>
    ///     (that are the AllowedTypes), <see cref="IAbstractPocoType.GenericArguments"/> and <see cref="IAbstractPocoType.Generalizations"/>
    ///     are not visited.
    ///     </item>
    ///     <item>
    ///     Collections visits their <see cref="ICollectionPocoType.ItemTypes"/>.
    ///     </item>
    ///     <item>
    ///     <see cref="IUnionPocoType"/> visits its <see cref="IOneOfPocoType.AllowedTypes"/>.
    ///     </item>
    ///     <item>
    ///     Visiting the <see cref="PocoTypeKind.Any"/> (like any other basic types) don't visit anything else.
    ///     </item>
    ///     <item>
    ///     <see cref="VisitEnum(IEnumPocoType)"/> does nothing (this doesn't visit the <see cref="IEnumPocoType.UnderlyingType"/>).
    ///     </item>
    ///     <item>
    ///     <see cref="IPocoType.ObliviousType"/> is not visited.
    ///     </item>
    /// </list>
    /// </para>
    /// </summary>
    public class PocoTypeVisitor<T> where T : class, IMinimalPocoTypeSet
    {
        readonly T _visited;

        /// <summary>
        /// Initializes a new visitor with an existing <see cref="LastVisited"/> instance.
        /// </summary>
        /// <param name="lastVisited">Already visited types.</param>
        public PocoTypeVisitor( T lastVisited )
        {
            Throw.CheckNotNullArgument( lastVisited );
            _visited = lastVisited;
        }

        /// <summary>
        /// Gets the type bag that have been visited once during the current
        /// or last <see cref="VisitRoot(IPocoType,bool)"/>.
        /// <para>
        /// Note that this set can contain both a nullable and its non nullable associated type if
        /// its implementation differentiates them. In practice, set implementations merge them: in such case
        /// this set can contain the nullable or the non nullable version of the same type but not both of them.
        /// </para>
        /// </summary>
        public T LastVisited => _visited;

        /// <summary>
        /// Starts a visit from type. This resets the <see cref="LastVisited"/> set by default.
        /// </summary>
        /// <param name="t">The type to visit.</param>
        /// <param name="clearLastVisited">False to keep the current <see cref="LastVisited"/> types.</param>
        /// <returns>The <see cref="LastVisited"/>.</returns>
        public T VisitRoot( IPocoType t, bool clearLastVisited = true )
        {
            if( clearLastVisited ) _visited.Clear();
            else if( _visited.Contains( t ) ) return _visited;

            OnStartVisit( t );
            Visit( t );
            return _visited;
        }

        /// <summary>
        /// Called by <see cref="VisitRoot(IPocoType, bool)"/> after having
        /// cleared the <see cref="LastVisited"/> and before the visit itself.
        /// <para>
        /// Does nothing by default.
        /// </para>
        /// </summary>
        /// <param name="root">The root type that will be visited.</param>
        protected virtual void OnStartVisit( IPocoType root )
        {
        }

        /// <summary>
        /// Dispatch the call to typed handler only if the type has not been already
        /// visited.
        /// </summary>
        /// <param name="t">The type to visit.</param>
        /// <returns>True if the type has been visited, false if it has been skipped (already visited).</returns>
        protected virtual bool Visit( IPocoType t )
        {
            if( !_visited.Add( t ) )
            {
                OnAlreadyVisited( t );
                return false;
            }
            switch( t )
            {
                case IPrimaryPocoType primary: VisitPrimaryPoco( primary ); break;
                case ISecondaryPocoType secondary: VisitSecondaryPoco( secondary ); break;
                case IAbstractPocoType abstractPoco: VisitAbstractPoco( abstractPoco ); break;
                case ICollectionPocoType collection: VisitCollection( collection ); break;
                case IRecordPocoType record: VisitRecord( record ); break;
                case IUnionPocoType union: VisitUnion( union ); break;
                case IEnumPocoType e: VisitEnum( e ); break;
                default:
                    Throw.DebugAssert( t.GetType().Name == "PocoType"
                                        || t.GetType().Name == "BasicValueTypeWithDefaultValue"
                                        || t.GetType().Name == "BasicRefType" );
                    VisitBasic( t );
                    break;
            }
            return true;
        }

        /// <summary>
        /// Called each time a type has been already visited.
        /// <para>
        /// Does nothing by default.
        /// </para>
        /// </summary>
        /// <param name="t">The already visited type.</param>
        protected virtual void OnAlreadyVisited( IPocoType t )
        {
        }

        /// <summary>
        /// Visits the <see cref="ICompositePocoType.Fields"/>, calling <see cref="VisitField(IPocoField)"/>.
        /// </summary>
        /// <param name="primary">A primary poco type.</param>
        protected virtual void VisitPrimaryPoco( IPrimaryPocoType primary )
        {
            foreach( var f in primary.Fields ) VisitField( f );
        }

        /// <summary>
        /// Visits the <see cref="ISecondaryPocoType.PrimaryPocoType"/>.
        /// </summary>
        /// <param name="secondary">A secondary poco type.</param>
        protected virtual void VisitSecondaryPoco( ISecondaryPocoType secondary )
        {
            VisitPrimaryPoco( secondary.PrimaryPocoType );
        }

        /// <summary>
        /// Visits the <see cref="IPocoField.Type"/>, calling <see cref="Visit(IPocoType)"/>.
        /// </summary>
        /// <param name="field">The record or poco field.</param>
        protected virtual void VisitField( IPocoField field )
        {
            Visit( field.Type );
        }

        /// <summary>
        /// Does nothing by default.
        /// </summary>
        /// <param name="abstractPoco">The abstract poco type.</param>
        protected virtual void VisitAbstractPoco( IAbstractPocoType abstractPoco )
        {
        }

        /// <summary>
        /// Visits the <see cref="ICollectionPocoType.ItemTypes"/>, calling <see cref="Visit(IPocoType)"/>
        /// for the collection type arguments.
        /// <para>
        /// This doesn't follow the <see cref="ICollectionPocoType.MutableCollection"/>: the arguments are the same.
        /// </para>
        /// </summary>
        /// <param name="collection">The collection to visit.</param>
        protected virtual void VisitCollection( ICollectionPocoType collection )
        {
            foreach( var itemType in collection.ItemTypes ) Visit( itemType );
        }

        /// <summary>
        /// Visits the <see cref="IOneOfPocoType.AllowedTypes"/>, calling <see cref="Visit(IPocoType)"/>
        /// </summary>
        /// <param name="union">The union type.</param>
        protected virtual void VisitUnion( IUnionPocoType union )
        {
            foreach( var itemType in union.AllowedTypes ) Visit( itemType );
        }

        /// <summary>
        /// Visits the <see cref="IRecordPocoType.Fields"/>, calling <see cref="VisitField(IPocoField)"/>.
        /// </summary>
        /// <param name="record">A record type.</param>
        /// 
        protected virtual void VisitRecord( IRecordPocoType record )
        {
            foreach( var f in record.Fields ) VisitField( f );
        }

        /// <summary>
        /// Does nothing by default (doesn't visit the <see cref="IEnumPocoType.UnderlyingType"/>).
        /// </summary>
        /// <param name="e">The enumeration type.</param>
        protected virtual void VisitEnum( IEnumPocoType e )
        {
        }

        /// <summary>
        /// <see cref="PocoTypeKind.Basic"/> or <see cref="PocoTypeKind.Any"/>.
        /// Does nothing by default.
        /// </summary>
        /// <param name="basic">The basic poco type. Can be a <see cref="IBasicRefPocoType"/>.</param>
        protected virtual void VisitBasic( IPocoType basic )
        {
        }
    }

}
