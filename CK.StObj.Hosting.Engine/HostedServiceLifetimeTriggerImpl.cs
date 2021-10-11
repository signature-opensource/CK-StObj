using CK.CodeGen;
using CK.Core;
using CK.Setup;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CK.StObj.Hosting.Engine
{
    public partial class HostedServiceLifetimeTriggerImpl : CSCodeGeneratorType
    {
        public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope type )
        {
            Debug.Assert( classType == typeof( HostedServiceLifetimeTrigger ) );

            if( c.ActualSourceCodeIsUseless ) return CSCodeGenerationResult.Success;

            using( monitor.OpenInfo( "Generating HostedServiceLifetimeTrigger implementation." ) )
            {
                type.Append( "readonly IServiceProvider _services;" ).NewLine()
                     .Append( "public " ).Append( type.Name ).Append( "( IServiceProvider s ) => _services = s;" ).NewLine();

                var types = c.CurrentRun.EngineMap.StObjs.OrderedStObjs;
                var startMethods = new List<(IStObjResult, MethodInfo)>();
                var stopMethods = new List<(IStObjResult, MethodInfo)>();

                // Uses logical or (no shortcut) to always evaluate all methods.
                if( !DiscoverStartOrStopMethod( monitor, types, HostedServiceLifetimeTrigger.StartMethodName, startMethods )
                    | !DiscoverStartOrStopMethod( monitor, types, HostedServiceLifetimeTrigger.StartMethodNameAsync, startMethods )
                    | !DiscoverStartOrStopMethod( monitor, types, HostedServiceLifetimeTrigger.StopMethodName, stopMethods )
                    | !DiscoverStartOrStopMethod( monitor, types, HostedServiceLifetimeTrigger.StopMethodNameAsync, stopMethods ) )
                {
                    return CSCodeGenerationResult.Failed;
                }

                using( monitor.OpenInfo( $"Generating 'StartAsync' method with {startMethods.Count} calls." ) )
                {
                    var method = classType.GetMethod( "StartAsync", BindingFlags.Instance | BindingFlags.Public );
                    Debug.Assert( method != null );
                    GenerateMethod( monitor, method, c.CurrentRun.EngineMap, type, startMethods );
                }

                using( monitor.OpenInfo( $"Generating 'StopAsync' method with {stopMethods.Count} calls." ) )
                {
                    stopMethods.Reverse();
                    var method = classType.GetMethod( "StopAsync", BindingFlags.Instance | BindingFlags.Public );
                    Debug.Assert( method != null );
                    GenerateMethod( monitor, method, c.CurrentRun.EngineMap, type, stopMethods );
                }
            }
            return CSCodeGenerationResult.Success;
        }

        void GenerateMethod( IActivityMonitor monitor, MethodInfo method, IStObjEngineMap map, ITypeScope scope, List<(IStObjResult, MethodInfo)> methods )
        {
            var m = scope.CreateOverride( method );
            if( methods.Count == 0 )
            {
                m.Append( "return Task.CompletedTask;" );
            }
            else
            {
                m.Definition.Modifiers |= Modifiers.Async;
                var requiredTypes = new TypeRegistrar( map );
                GenerateCode( monitor, m, methods, requiredTypes );
            }
        }

        void GenerateCode( IActivityMonitor monitor, IFunctionScope body, List<(IStObjResult T, MethodInfo M)> methods, TypeRegistrar requiredTypes )
        {
            body.Append( "var g = (" ).Append(StObjContextRoot.RootContextTypeName).Append( ")_services.GetService( typeof(IStObjMap) );" ).NewLine();
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
                    body.Append( "await (" ).Append( m.M.ReturnType == typeof(ValueTask) ? "(ValueTask)" : "(Task)" );
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


        bool DiscoverStartOrStopMethod( IActivityMonitor monitor, IEnumerable<IStObjResult> all, string methodName, List<(IStObjResult,MethodInfo)> collector )
        {
            bool success = true;
            foreach( var r in all )
            {
                var t = r.ClassType;
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
                        if( m.ReturnType != typeof(Task) && m.ReturnType != typeof(ValueTask) )
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
            return success;
        }
    }
}
