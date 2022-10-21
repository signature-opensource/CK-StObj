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
    /// By default, <see cref="IPocoField"/>, <see cref="ICollectionPocoType.ItemTypes"/> and
    /// <see cref="IConcretePocoType.PrimaryInterface"/> are followed.
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
        /// or last <see cref="VisitRoot(IPocoType)"/>.
        /// </summary>
        public IReadOnlySet<IPocoType> LastVisited => _visited;

        /// <summary>
        /// Starts a visit from type. This resets the <see cref="LastVisited"/>.
        /// </summary>
        /// <param name="t">The type to visit.</param>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>The <see cref="LastVisited"/>.</returns>
        public IReadOnlySet<IPocoType> VisitRoot( IActivityMonitor monitor, IPocoType t )
        {
            _visited.Clear();
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
                case IConcretePocoType poco: VisitConcretePoco( monitor, poco ); break;
                case IAbstractPocoType abstractPoco: VisitAbstractPoco( monitor, abstractPoco ); break;
                case ICollectionPocoType collection: VisitCollection( monitor, collection ); break;
                case IRecordPocoType record: VisitRecord( monitor, record ); break;
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
        /// Visits the <see cref="IConcretePocoField.Fields"/>, calling <see cref="VisitField(IActivityMonitor, ICompositePocoType, IPocoField)"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="primary">A primary poco type.</param>
        protected virtual void VisitPrimaryPoco( IActivityMonitor monitor, IPrimaryPocoType primary )
        {
            foreach( var f in primary.Fields ) VisitField( monitor, primary, f );
        }

        /// <summary>
        /// Visits the <see cref="IPocoField.Type"/>, calling <see cref="Visit(IActivityMonitor, IPocoType)"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="field">The field's owner.</param>
        /// <param name="field">The record or poco field.</param>
        protected virtual void VisitField( IActivityMonitor monitor, ICompositePocoType owner, IPocoField field )
        {
            Visit( monitor, field.Type );
        }

        /// <summary>
        /// Visits the <see cref="IConcretePocoType.PrimaryInterface"/>, calling <see cref="VisitPrimaryPoco(IActivityMonitor, IPrimaryPocoType)"/>
        /// that visits the fields.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="poco">The poco concrete type.</param>
        protected virtual void VisitConcretePoco( IActivityMonitor monitor, IConcretePocoType poco )
        {
            VisitPrimaryPoco( monitor, poco.PrimaryInterface );
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
            foreach( var itemType in collection.ItemTypes ) Visit( monitor, itemType);
        }

        /// <summary>
        /// Visits the <see cref="IRecordPocoType.Fields"/>, calling <see cref="VisitField(IActivityMonitor, ICompositePocoType, IPocoField)"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="record">A record type.</param>
        protected virtual void VisitRecord( IActivityMonitor monitor, IRecordPocoType record)
        {
            foreach( var f in record.Fields ) VisitField( monitor, record, f );
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
