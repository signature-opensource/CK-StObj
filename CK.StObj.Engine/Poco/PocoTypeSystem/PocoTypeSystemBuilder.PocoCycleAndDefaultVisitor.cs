using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CK.Setup
{
    partial class PocoTypeSystemBuilder
    {
        internal sealed class PocoCycleAndDefaultVisitor : PocoTypeVisitor
        {
            // Used to compute a readable path for anonymous types. This displays detected cycles like this:
            //
            // Detected an instantiation cycle in Poco: 
            // '[IPoco]RecursivePocoTests.IHolder', field: 'Pof.DeepInside.Inside.IAmHere' => 
            // '[IPoco]RecursivePocoTests.IOther', field: 'Pof.Inside.IAmHere' => 'RecursivePocoTests.IHolder'.
            //
            List<List<IPocoField>> _path;
            int _typedPathCount;
            // Both will stop the visit.
            bool _cycleFound;
            bool _missingDefault;

            public PocoCycleAndDefaultVisitor()
            {
                _path = new List<List<IPocoField>>();
            }

            public bool CheckValid( IActivityMonitor monitor, ref bool cycleError )
            {
                if( _typedPathCount > 0 )
                {
                    Throw.DebugAssert( _cycleFound || _missingDefault );
                    if( _cycleFound && !cycleError )
                    {
                        cycleError = true;
                        var cycle = _path.Select( c => $"{Environment.NewLine}'[{c[0].Owner.Kind}]{c[0].Owner.CSharpName}', field: '{c.Select( f => f.Name ).Concatenate( "." )}' => " );
                        monitor.Error( $"Detected an instantiation cycle in Poco: {cycle.Concatenate( "" )}'[{_path[0][0].Owner.Kind}]{_path[0][0].Owner.CSharpName}'." );
                    }
                    if( _missingDefault )
                    {
                        // Keep only the disallowed fields.
                        foreach( var fields in _path ) fields.RemoveAll( f => !f.DefaultValueInfo.IsDisallowed );
                        // Keep only the non empty paths.
                        _path.RemoveAll( fields => fields.Count == 0 );
                        Throw.DebugAssert( _path.Count >= 1 );
                        var missing = _path.Select( c => $"'[{c[0].Owner.Kind}]{c[0].Owner.CSharpName}', field: '{c.Select( f => f.Name ).Concatenate( "." )}' has no default value." );
                        var last = _path[^1][^1];
                        monitor.Error( $"Required computable default value is missing in Poco:" +
                                       $"{Environment.NewLine}{missing.Concatenate( $"{Environment.NewLine}Because " )}" +
                                       $"{Environment.NewLine}No default can be synthesized for non nullable '[{last.Type.Kind}]{last.Type.CSharpName}'." );
                    }
                    return false;
                }
                return true;
            }

            protected override void OnStartVisit( IPocoType root )
            {
                // Reuse the allocated lists as much as possible.
                for( int i = 0; i < _typedPathCount; i++ ) _path[i].Clear();
                _typedPathCount = 0;
                _cycleFound = false;
                _missingDefault = false;
            }

            protected override bool Visit( IPocoType t )
            {
                // Shortcut the visit if a cycle or a missing default has been found.
                if( _cycleFound || _missingDefault ) return true;
                return base.Visit( t );
            }

            protected override void OnAlreadyVisited( IPocoType t )
            {
                _cycleFound |= t.Kind == PocoTypeKind.PrimaryPoco | t.Kind == PocoTypeKind.SecondaryPoco;
            }

            protected override void VisitCollection( ICollectionPocoType collection )
            {
                // We are not interested in collection items: their initialization
                // is under the responsibility of the user code.
            }

            protected override void VisitUnion( IUnionPocoType union )
            {
                // We are not interested in union type variants since we don't have a [UnionTypeDefault(...)]
                // or a [UnionType<T>( IsDefault = true)] (yet?).
            }

            protected override void VisitField( IPocoField field )
            {
                if( _cycleFound || _missingDefault ) return;
                // It's only if the field requires an initialization that we
                // should follow the path.
                if( field.DefaultValueInfo.RequiresInit )
                {
                    bool isAnonymous = PushPath( field );
                    base.VisitField( field );
                    if( !_cycleFound ) PopPath( isAnonymous );
                }
                else if( field.DefaultValueInfo.IsDisallowed )
                {
                    PushPath( field );
                    // Enters the field to try to find the inner culprit if it exists
                    // but stops the visit once done.
                    // We may find both a cycle and a missing default.
                    base.VisitField( field );
                    _missingDefault = true;
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
                    Throw.DebugAssert( _path[_typedPathCount - 1].Count == 1 );
                    _path[_typedPathCount - 1].Clear();
                    --_typedPathCount;
                }
            }

            bool PushPath( IPocoField field )
            {
                bool isAnonymous = field.Owner is IRecordPocoType r && r.IsAnonymous;
                if( isAnonymous )
                {
                    Throw.DebugAssert( _typedPathCount > 0 );
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
                        Throw.DebugAssert( _path[_typedPathCount].Count == 0 );
                        _path[_typedPathCount].Add( field ); 
                    }
                    _typedPathCount++;
                }
                return isAnonymous;
            }
        }
    }

}



