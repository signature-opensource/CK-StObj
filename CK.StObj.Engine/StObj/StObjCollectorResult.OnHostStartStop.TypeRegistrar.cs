using CK.CodeGen;
using CK.Core;
using CK.Setup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

#nullable enable

namespace CK.Setup
{
    public partial class StObjCollectorResult
    {
        class TypeRegistrar
        {
            readonly Dictionary<Type, (int Idx, bool Opt)> _mapping;
            readonly IStObjEngineMap _map;
            readonly Dictionary<string, string> _parametersArray;

            public TypeRegistrar( IStObjEngineMap map )
            {
                _mapping = new Dictionary<Type, (int, bool)>();
                _map = map;
                _parametersArray = new Dictionary<string, string>();
                // The identifier 1 is required IActivityMonitor.
                _mapping.Add( typeof( IActivityMonitor ), (1, false) );
            }

            public int Requires( Type t, bool optional )
            {
                if( t == typeof( CancellationToken ) ) return 0;
                // Uses static mapping first.
                t = _map.StObjs.ToLeaf( t )?.ClassType
                        ?? _map.Services.SimpleMappings.GetValueOrDefault( t )?.ClassType
                        ?? _map.Services.ManualMappings.GetValueOrDefault( t )?.ClassType
                        ?? t;
                if( !_mapping.TryGetValue( t, out var e ) )
                {
                    _mapping.Add( t, e = (_mapping.Count + 1, optional) );
                }
                else
                {
                    if( !optional && e.Opt ) _mapping[t] = (e.Idx, false);
                }
                return e.Idx;
            }

            public string GetParametersArray( IEnumerable<ParameterInfo> parameters )
            {
                var indices = parameters.Select( p => Requires( p.ParameterType, p.HasDefaultValue ) ).ToList();
                var varName = "v" + string.Join( "_", indices );
                if( !_parametersArray.TryGetValue( varName, out var definition ) )
                {
                    definition = string.Join( ", ", indices.Select( i => i == 0 ? "cancel" : (i == 1 ? "monitor" : "s" + i) ) );
                    _parametersArray.Add( varName, definition );
                }
                return varName;
            }

            public void WriteDeclarations( ICodeWriter declSpace )
            {
                declSpace.GeneratedByComment();
                foreach( var (t, (idx, opt)) in _mapping )
                {
                    if( idx != 1 )
                    {
                        declSpace.Append( "var s" ).Append( idx )
                             .Append( " = scope.ServiceProvider.GetService<" ).AppendCSharpName( t ).Append( ">( " ).Append( !opt ).Append( " );" ).NewLine();
                    }
                }
                foreach( var (name, def) in _parametersArray )
                {
                    declSpace.Append( "var " ).Append( name ).Append( " = new object[]{ " ).Append( def ).Append( " };" ).NewLine();
                }
            }
        }
    }
}
