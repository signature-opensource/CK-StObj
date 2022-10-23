using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CK.Setup
{
    partial class PocoType
    {
        internal sealed class InstantiationCycleVisitor : PocoTypeVisitor
        {
            // Used to compute a readable path for anonymous types. This displays detected cycles like this:
            //
            // Detected an instantiation cycle in Poco: 
            // '[IPoco]RecursivePocoTests.IHolder', field: 'Pof.DeepInside.Inside.IAmHere' => 
            // '[IPoco]RecursivePocoTests.IOther', field: 'Pof.Inside.IAmHere' => 'RecursivePocoTests.IHolder'.
            //
            List<List<IPocoField>> _path;
            int _typedPathCount;
            // Will stop the 
            bool _cycleFound;

            public InstantiationCycleVisitor()
            {
                _path = new List<List<IPocoField>>();
            }

            public bool CheckValid( IActivityMonitor monitor )
            {
                if( _typedPathCount > 0 )
                {
                    var cycle = _path.Select( c => $"{Environment.NewLine}'{c[0].Owner}', field: '{c.Select( f => f.Name ).Concatenate( "." )}' => " );
                    monitor.Error( $"Detected an instantiation cycle in Poco: {cycle.Concatenate( "" )}'{_path[0][0].Owner}'." );
                    return false;
                }
                return true;
            }

            protected override void OnStartVisit( IActivityMonitor monitor, IPocoType root )
            {
                // Reuse the allocated lists as much as possible.
                for( int i = 0; i < _typedPathCount; i++ ) _path[i].Clear();
                _typedPathCount = 0;
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

            protected override void VisitUnion( IActivityMonitor monitor, IUnionPocoType union )
            {
                // We are not interested in union type variants since we don't have a [UnionTypeDefault(...)]
                // or a [UnionType<T>( IsDefault = true)] (yet?).
            }

            protected override void VisitField( IActivityMonitor monitor, IPocoField field )
            {
                // It's only if the field requires an initialization that we
                // should follow the path.
                if( field.DefaultValueInfo.RequiresInit )
                {
                    bool isAnonymous = PushPath( field );
                    base.VisitField( monitor, field );
                    if( !_cycleFound ) PopPath( isAnonymous );
                }
            }

            void PopPath( bool isAnonymous )
            {
                Debug.Assert( _typedPathCount > 0 && _path.Count >= _typedPathCount );
                if( isAnonymous )
                {
                    var p = _path[_typedPathCount - 1];
                    p.RemoveAt( p.Count - 1 );
                }
                else
                {
                    Debug.Assert( _path[_typedPathCount - 1].Count == 1 );
                    _path[_typedPathCount - 1].Clear();
                    --_typedPathCount;
                }
            }

            bool PushPath( IPocoField field )
            {
                bool isAnonymous = field.Owner is IRecordPocoType r && r.IsAnonymous;
                if( isAnonymous )
                {
                    Debug.Assert( _typedPathCount > 0 );
                    _path[_typedPathCount - 1].Add( field );
                }
                else
                {
                    if( _typedPathCount == _path.Count )
                    {
                        _path.Add( new List<IPocoField> { field } );
                    }
                    else
                    {
                        Debug.Assert( _path[_typedPathCount].Count == 0 );
                        _path[_typedPathCount].Add( field ); 
                    }
                    _typedPathCount++;
                }
                return isAnonymous;
            }
        }
    }

}



