using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace CK.Setup
{
    public partial class StObjCollectorResult
    {
        sealed class HostedServiceLifetimeTriggerImpl
        {
            readonly List<(IStObjResult, MethodInfo)> _starts;
            readonly List<(IStObjResult, MethodInfo)> _stops;

            HostedServiceLifetimeTriggerImpl( List<(IStObjResult, MethodInfo)> starts, List<(IStObjResult, MethodInfo)> stops )
            {
                _starts = starts;
                _stops = stops;
            }

            public void GenerateHostedServiceLifetimeTrigger( IActivityMonitor monitor, IStObjEngineMap map, ITypeDefinerScope code )
            {
                code.GeneratedByComment();
                var c = code.CreateType( "sealed class HostedServiceLifetimeTrigger : Microsoft.Extensions.Hosting.IHostedService" )
                            .Append( "readonly IServiceProvider _services;" ).NewLine()
                            .Append( "public HostedServiceLifetimeTrigger( IServiceProvider s ) => _services = s;" ).NewLine();
                var start = c.CreateFunction( "public Task StartAsync( System.Threading.CancellationToken cancel )" );
                var stop = c.CreateFunction( "public Task StopAsync( System.Threading.CancellationToken cancel )" );

                using( monitor.OpenInfo( $"Generating 'StartAsync' method with {_starts.Count} calls." ) )
                {
                    GenerateMethod( monitor, start, map, _starts );
                }
                using( monitor.OpenInfo( $"Generating 'StopAsync' method with {_stops.Count} calls." ) )
                {
                    GenerateMethod( monitor, stop, map, _stops );
                }
            }

            void GenerateMethod( IActivityMonitor monitor, IFunctionScope m, IStObjEngineMap map, List<(IStObjResult, MethodInfo)> methods )
            {
                if( methods.Count == 0 )
                {
                    m.Append( "return Task.CompletedTask;" );
                }
                else
                {
                    var requiredTypes = new TypeRegistrar( map );
                    GenerateMethodCode( monitor, m, methods, requiredTypes, out var asyncRequires );
                    if( asyncRequires )
                    {
                        m.Definition.Modifiers |= Modifiers.Async;
                    }
                }
            }


            void GenerateMethodCode( IActivityMonitor monitor,
                                     IFunctionScope body,
                                     List<(IStObjResult T, MethodInfo M)> methods,
                                     TypeRegistrar requiredTypes,
                                     out bool asyncRequires )
            {
                asyncRequires = false;
                body.Append( "var g = (" ).Append( StObjContextRoot.RootContextTypeName ).Append( ")_services.GetService( typeof(IStObjMap) );" ).NewLine();
                body.Append( "using var scope = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.CreateScope( _services );" ).NewLine();
                body.Append( "var monitor = scope.ServiceProvider.GetService<IActivityMonitor>( true );" ).NewLine();
                var callings = string.Join( " and ", methods.GroupBy( x => x.M.Name ).Select( g => $"{g.Count()} '{g.Key}' method{(g.Count() > 1 ? "s" : "")}" ) );
                body.Append( "using( monitor.OpenInfo( \"Calling: " ).Append( callings ).Append( ".\" ) )" ).NewLine()
                    .Append( "{" ).NewLine();
                var declSpace = body.CreatePart();
                body.GeneratedByComment();
                foreach( var m in methods )
                {
                    monitor.Trace( $"Generating call to '{m.T.ClassType.ToString()}.{m.M.Name}'." );
                    bool isAsync = m.M.Name.EndsWith( "Async" );
                    if( isAsync )
                    {
                        asyncRequires = true;
                        body.Append( "await (" ).Append( m.M.ReturnType == typeof( ValueTask ) ? "(ValueTask)" : "(Task)" );
                    }
                    body.AppendTypeOf( m.T.ClassType ).Append( ".GetMethod( " ).AppendSourceString( m.M.Name ).Append( " , BindingFlags.Instance | BindingFlags.NonPublic )" )
                                                      .Append( ".Invoke( g.InternalRealObjects[" ).Append( m.T.IndexOrdered ).Append( "].FinalImplementation.Implementation, " )
                                                      .Append( requiredTypes.GetParametersArray( m.M.GetParameters() ) ).Append( " )" );
                    if( isAsync )
                    {
                        body.Append( ").ConfigureAwait( false )" );
                    }
                    body.Append( ";" ).NewLine();
                }
                body.Append( "}" ).NewLine();
                requiredTypes.WriteDeclarations( declSpace );
            }


            public static HostedServiceLifetimeTriggerImpl? DiscoverMethods( IActivityMonitor monitor, IStObjEngineMap map )
            {
                using( monitor.OpenInfo( "Discovering OnHostStart/Stop[Async] methods on real objects and generating HostedServiceLifetimeTrigger implementation." ) )
                {
                    var types = map.StObjs.OrderedStObjs;
                    var startMethods = new List<(IStObjResult, MethodInfo)>();
                    var stopMethods = new List<(IStObjResult, MethodInfo)>();

                    if( !DiscoverStartOrStopMethod( monitor, types, startMethods, stopMethods ) )
                    {
                        monitor.CloseGroup( "Failed." );
                        return null;
                    }
                    stopMethods.Reverse();
                    if( startMethods.Count == 0 && stopMethods.Count == 0 )
                    {
                        monitor.CloseGroup( $"No OnHostStart/Stop[Async] method found on the {types.Count} real objects. HostedServiceLifetimeTrigger will only deal with the EndpointTypeManager." );
                    }
                    else
                    {
                        monitor.CloseGroup( $"Found {startMethods.Count} OnHostStart[Async] method(s) and {stopMethods.Count} OnHostStop[Async] found on the {types.Count} real objects." );
                    }
                    return new HostedServiceLifetimeTriggerImpl( startMethods, stopMethods );
                }

            }

            static bool DiscoverStartOrStopMethod( IActivityMonitor monitor,
                                                   IEnumerable<IStObjResult> all,
                                                   List<(IStObjResult, MethodInfo)> startMethods,
                                                   List<(IStObjResult, MethodInfo)> stopMethods )
            {
                bool success = true;
                foreach( var r in all )
                {
                    var t = r.ClassType;
                    ProcessMethod( monitor, StObjContextRoot.StartMethodName, startMethods, ref success, r, t );
                    ProcessMethod( monitor, StObjContextRoot.StartMethodNameAsync, startMethods, ref success, r, t );
                    ProcessMethod( monitor, StObjContextRoot.StopMethodName, stopMethods, ref success, r, t );
                    ProcessMethod( monitor, StObjContextRoot.StopMethodNameAsync, stopMethods, ref success, r, t );
                }
                return success;

                static void ProcessMethod( IActivityMonitor monitor, string methodName, List<(IStObjResult, MethodInfo)> collector, ref bool success, IStObjResult r, Type t )
                {
                    var m = t.GetMethod( methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.Static );
                    if( m != null )
                    {
                        if( m.IsPublic || m.IsStatic )
                        {
                            monitor.Error( $"Method '{t.ToCSharpName()}.{m.Name}' must be a private instance method." );
                            success = false;
                        }
                        if( methodName.EndsWith( "Async" ) )
                        {
                            if( m.ReturnType != typeof( Task ) && m.ReturnType != typeof( ValueTask ) )
                            {
                                monitor.Error( $"Method '{t.ToCSharpName()}.{m.Name}' must return a Task or a ValueTask since it ends with \"Async\"." );
                                m = null;
                            }
                        }
                        else
                        {
                            if( m.ReturnType != typeof( void ) )
                            {
                                monitor.Error( $"Method '{t.ToCSharpName()}.{m.Name}' must be void since it doesn't end with \"Async\"." );
                                m = null;
                            }
                        }
                        if( m == null )
                        {
                            success = false;
                        }
                        else
                        {
                            Debug.Assert( m.DeclaringType == t );
                            collector.Add( (r, m) );
                        }
                    }
                }
            }

        }
    }
}
