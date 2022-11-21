using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace CK.Setup
{
    /// <summary>
    /// Safe <see cref="IPocoType"/> base visitor (a type is visited only once), visited types
    /// are available in <see cref="LastVisited"/>.
    /// By default, <see cref="IPocoField"/>, <see cref="ICollectionPocoType.ItemTypes"/> and <see cref="IUnionPocoType"/>'s
    /// <see cref="IOneOfPocoType{T}.AllowedTypes"/> are followed.
    /// </summary>
    public class PocoTypeVisitor
    {
        readonly HashSet<IPocoType> _visited;

        /// <summary>
        /// Initializes a new visitor.
        /// </summary>
        public PocoTypeVisitor()
        {
            _visited = new HashSet<IPocoType>();
        }

        /// <summary>
        /// Gets the types that have been visited once during the current
        /// or last <see cref="VisitRoot(IActivityMonitor, IPocoType)"/>.
        /// </summary>
        public IReadOnlySet<IPocoType> LastVisited => _visited;

        /// <summary>
        /// Starts a visit from type. This resets the <see cref="LastVisited"/> by default.
        /// </summary>
        /// <param name="t">The type to visit.</param>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="resetLastVisited">False to keep the current <see cref="LastVisited"/>.</param>
        /// <returns>The <see cref="LastVisited"/>.</returns>
        public IReadOnlySet<IPocoType> VisitRoot( IActivityMonitor monitor, IPocoType t, bool resetLastVisited = true )
        {
            if( resetLastVisited ) _visited.Clear();
            else if( _visited.Contains( t ) ) return _visited;

            OnStartVisit( monitor, t );
            Visit( monitor, t );
            return _visited;
        }

        /// <summary>
        /// Called by <see cref="VisitRoot(IActivityMonitor, IPocoType)"/> after having
        /// cleared the <see cref="LastVisited"/> and before the visit itself.
        /// <para>
        /// Does nothing by default.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="root">The root type that will be visited.</param>
        protected virtual void OnStartVisit( IActivityMonitor monitor, IPocoType root )
        {
        }

        /// <summary>
        /// Dispatch the call to typed handler only if the type has not been already
        /// visited.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="t">The type to visit.</param>
        /// <returns>True if the type has been visited, false if it has been skipped (already visited).</returns>
        protected virtual bool Visit( IActivityMonitor monitor, IPocoType t )
        {
            if( !_visited.Add( t ) )
            {
                OnAlreadyVisited( monitor, t );
                return false;
            }
            switch( t )
            {
                case IPrimaryPocoType primary: VisitPrimaryPoco( monitor, primary ); break;
                case IAbstractPocoType abstractPoco: VisitAbstractPoco( monitor, abstractPoco ); break;
                case ICollectionPocoType collection: VisitCollection( monitor, collection ); break;
                case IRecordPocoType record: VisitRecord( monitor, record ); break;
                case IUnionPocoType union: VisitUnion( monitor, union ); break;
                default:
                    Debug.Assert( t.GetType().Name == "PocoType" || t.GetType().Name == "BasicTypeWithDefaultValue" );
                    VisitBasic( monitor, t );
                    break;
            };
            return true;
        }

        /// <summary>
        /// Called each time a type has been already visited.
        /// <para>
        /// Does nothing by default.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="t">The already visited type.</param>
        protected virtual void OnAlreadyVisited( IActivityMonitor monitor, IPocoType t )
        {
        }

        /// <summary>
        /// Visits the <see cref="ICompositePocoType.Fields"/>, calling <see cref="VisitField(IActivityMonitor, IPocoField)"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="primary">A primary poco type.</param>
        protected virtual void VisitPrimaryPoco( IActivityMonitor monitor, IPrimaryPocoType primary )
        {
            foreach( var f in primary.Fields ) VisitField( monitor, f );
        }

        /// <summary>
        /// Visits the <see cref="IPocoField.Type"/>, calling <see cref="Visit(IActivityMonitor, IPocoType)"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="field">The record or poco field.</param>
        protected virtual void VisitField( IActivityMonitor monitor, IPocoField field )
        {
            Visit( monitor, field.Type );
        }

        /// <summary>
        /// Does nothing by default.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="abstractPoco">The abstract poco type.</param>
        protected virtual void VisitAbstractPoco( IActivityMonitor monitor, IAbstractPocoType abstractPoco )
        {
        }

        /// <summary>
        /// Visits the <see cref="ICollectionPocoType.ItemTypes"/>, calling <see cref="Visit(IActivityMonitor, IPocoType)"/>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="collection"></param>
        protected virtual void VisitCollection( IActivityMonitor monitor, ICollectionPocoType collection )
        {
            foreach( var itemType in collection.ItemTypes ) Visit( monitor, itemType );
        }

        /// <summary>
        /// Visits the <see cref="IOneOfPocoType{T}.AllowedTypes"/>, calling <see cref="Visit(IActivityMonitor, IPocoType)"/>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="union"></param>
        protected virtual void VisitUnion( IActivityMonitor monitor, IUnionPocoType union )
        {
            foreach( var itemType in union.AllowedTypes ) Visit( monitor, itemType );
        }

        /// <summary>
        /// Visits the <see cref="IRecordPocoType.Fields"/>, calling <see cref="VisitField(IActivityMonitor, IPocoField)"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="record">A record type.</param>
        protected virtual void VisitRecord( IActivityMonitor monitor, IRecordPocoType record )
        {
            foreach( var f in record.Fields ) VisitField( monitor, f );
        }

        /// <summary>
        /// Does nothing by default.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="basic">The basic poco type.</param>
        protected virtual void VisitBasic( IActivityMonitor monitor, IPocoType basic )
        {
        }
    }

}
