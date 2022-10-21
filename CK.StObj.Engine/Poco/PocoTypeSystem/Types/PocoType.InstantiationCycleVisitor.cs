using CK.Core;
using System.Collections.Generic;
using System.Diagnostics;

namespace CK.Setup
{
    partial class PocoType
    {
        internal sealed class InstantiationCycleVisitor : PocoTypeVisitor
        {
            List<(ICompositePocoType Typed, LinkedList<IPocoField> FieldPath)> _path;
            bool _cycleFound;

            public InstantiationCycleVisitor()
            {
                _path = new List<(ICompositePocoType, LinkedList<IPocoField>)>();
            }

            public IReadOnlyList<(ICompositePocoType Typed, LinkedList<IPocoField> FieldPath)> Cycle => _path;

            protected override void OnStartVisit( IActivityMonitor monitor, IPocoType root )
            {
                _path.Clear();
                _cycleFound = false;
            }

            protected override bool Visit( IActivityMonitor monitor, IPocoType t )
            {
                // Shortcut the visit if a cycle has been found.
                if( _cycleFound ) return true;
                return base.Visit( monitor, t );
            }

            protected override void OnAlreadyVisited( IActivityMonitor monitor, IPocoType t )
            {
                _cycleFound |= t.Kind == PocoTypeKind.IPoco;
            }

            protected override void VisitCollection( IActivityMonitor monitor, ICollectionPocoType collection )
            {
                // We are not interested in collection items: their initialization
                // is under the responsibility of the user code.
            }

            protected override void VisitField( IActivityMonitor monitor, ICompositePocoType owner, IPocoField field )
            {
                // It's only if the field requires an initialization that we
                // should follow the path.
                if( field.DefaultValueInfo.RequiresInit )
                {
                    bool isAnonymous = PushPath( owner, field );
                    base.VisitField( monitor, owner, field );
                    if( !_cycleFound ) PopPath( isAnonymous );
                }
            }

            void PopPath( bool isAnonymous )
            {
                if( isAnonymous )
                {
                    _path[^1].FieldPath.RemoveLast();
                }
                else
                {
                    _path.RemoveAt( _path.Count - 1 );
                }
            }

            bool PushPath( ICompositePocoType owner, IPocoField field )
            {
                bool isAnonymous = owner is IRecordPocoType r && r.IsAnonymous;
                if( isAnonymous )
                {
                    Debug.Assert( _path.Count > 0 );
                    _path[^1].FieldPath.AddLast( field );
                }
                else
                {
                    var fieldPath = new LinkedList<IPocoField>();
                    fieldPath.AddFirst( field );
                    _path.Add( (owner, fieldPath) );
                }

                return isAnonymous;
            }
        }
    }

}



