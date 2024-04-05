using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace CK.Setup
{
    public partial class StObjCollector
    {
        class SCRClass
        {
            public readonly AutoServiceClassInfo Class;
            public readonly InterfaceFamily Family;

            List<CtorParameter>? _params;
            bool _isHeadCandidate;
            bool _isHead;

            public class CtorParameter
            {
                public readonly AutoServiceClassInfo.CtorParameter Parameter;
                bool _canUseDefault;

                public bool HasDefault => Parameter.ParameterInfo.HasDefaultValue;

                public bool CanUseDefault
                {
                    get => _canUseDefault;
                    set
                    {
                        Debug.Assert( value == false || HasDefault );
                        _canUseDefault = value;
                    }
                }

                public CtorParameter( AutoServiceClassInfo.CtorParameter p )
                {
                    Parameter = p;
                }

                public override string ToString() => Parameter.ToString();
            }

            public SCRClass( InterfaceFamily f, AutoServiceClassInfo c )
            {
                Family = f;
                Class = c;
            }

            public IReadOnlyList<CtorParameter> Parameters => _params!;

            internal bool Initialize( IActivityMonitor m )
            {
                Debug.Assert( Class.ConstructorParameters != null );
                bool success = true;
                _params = new List<CtorParameter>();
                foreach( var p in Class.ConstructorParameters )
                {
                    if( p.ServiceInterface != null && Family.Interfaces.Contains( p.ServiceInterface ))
                    {
                        if( !p.IsEnumerable )
                        {
                            m.Error( $"Invalid parameter {p}: it can not be an Auto Service of its own family." );
                            success = false;
                        }
                    }
                }
                if( success )
                {
                Debug.Assert( Class.Interfaces != null );
                    _isHeadCandidate = !Family.Interfaces.Except( Class.Interfaces ).Any();
                    _isHead = _isHeadCandidate
                              && Family.Classes.Where( c => c != this )
                                               .All( c => Class.ComputedCtorParametersClassClosure.Contains( c.Class ) );
                }
                return success;
            }

            /// <summary>
            /// A head candidate is a class that implements all its <see cref="Family"/>'s
            /// <see cref="InterfaceFamily.Interfaces"/>.
            /// </summary>
            public bool IsHeadCandidate => _isHeadCandidate;

            /// <summary>
            /// To be a head, this class must be a head candidate and its constructor parameter closure must
            /// cover all other <see cref="Family"/>'s <see cref="InterfaceFamily.Classes"/>.
            /// </summary>
            public bool IsHead => _isHead;

            public override string ToString() => Class.ToString();
        }

        sealed class InterfaceFamily
        {
            readonly HashSet<AutoServiceInterfaceInfo> _interfaces;
            readonly Dictionary<AutoServiceClassInfo,SCRClass> _classes;

            public IReadOnlyCollection<AutoServiceInterfaceInfo> Interfaces => _interfaces;

            public IReadOnlyCollection<SCRClass> Classes => _classes.Values;

            public AutoServiceClassInfo? Resolved { get; private set; }

            InterfaceFamily()
            {
                _interfaces = new HashSet<AutoServiceInterfaceInfo>();
                _classes = new Dictionary<AutoServiceClassInfo, SCRClass>();
            }

            bool InitializeClasses( IActivityMonitor monitor )
            {
                Debug.Assert( Classes.Count > 0 );
                bool success = true;
                foreach( var c in Classes )
                {
                    success &= c.Initialize( monitor );
                }
                return success;
            }

            public bool Resolve( IActivityMonitor monitor )
            {
                bool success = true;
                Debug.Assert( Classes.Count > 0 && Interfaces.Count > 0 );
                if( Classes.Count == 1 )
                {
                    Resolved = Classes.Single().Class;
                }
                else
                {
                    using( monitor.OpenInfo( $"Service resolution required for {ToString()}." ) )
                    {
                        if( success = InitializeClasses( monitor ) )
                        {
                            var headCandidates = Classes.Where( c => c.IsHeadCandidate ).ToList();
                            var heads = headCandidates.Where( c => c.IsHead ).ToList();
                            if( headCandidates.Count == 0 )
                            {
                                monitor.Error( $"No possible implementation found. A class that implements '{BaseInterfacesToString()}' interfaces is required." );
                                success = false;
                            }
                            else if( heads.Count == 0 )
                            {
                                monitor.Error( $"Among '{headCandidates.Select( c => c.ToString() ).Concatenate( "', '" )}' possible implementations, none covers the whole set of other implementations. Use [ReplaceAutoService(...)] attribute to disambiguate." );
                                var couldUseStObjConstruct = headCandidates.Select( c => c.Class.TypeInfo )
                                                                   .OfType<RealObjectClassInfo>()
                                                                   .Where( c => c.ConstructParameters != null
                                                                                && c.ConstructParameters
                                                                                        .Any( p => headCandidates.Select( x => x.Class.ClassType ).Any( o => p.ParameterType.IsAssignableFrom( o ) ) ) )
                                                                   .Select( c => $"{c.Type.FullName}.StObjConstruct( {c.ConstructParameters.Select( p => p.ParameterType.Name ).Concatenate() } )" )
                                                                   .FirstOrDefault();

                                if( couldUseStObjConstruct != null )
                                {
                                    monitor.Error( $"Please note that RealObject.StObjConstruct parameters are irrelevant to Service resolution: for instance {couldUseStObjConstruct} is ignored. Use [ReplaceAutoService(...)] attribute." );
                                }
                                success = false;
                            }
                            else if( heads.Count > 1 )
                            {
                                monitor.Error( $"Multiple possible implementations found: '{heads.Select( c => c.ToString() ).Concatenate( "', '" )}'. They must be unified." );
                                success = false;
                            }
                            else
                            {
                                var r = heads[0].Class;
                                Resolved = r;
                                monitor.CloseGroup( $"Resolved to '{r}'." );
                            }
                        }
                    }
                }
                if( success )
                {
                    foreach( var i in _interfaces )
                    {
                        i.FinalResolved = Resolved;
                    }
                }
                return success;
            }

            public static IReadOnlyCollection<InterfaceFamily> Build( IActivityMonitor m,
                                                                      IEnumerable<AutoServiceClassInfo> classes )
            {
                var families = new Dictionary<AutoServiceInterfaceInfo, InterfaceFamily>();
                bool familiesHasBeenMerged = false;
                foreach( var c in classes )
                {
                    Debug.Assert( c.IsIncluded
                                  && c.Interfaces != null
                                  && (c.Interfaces.Count == 0 || c.Interfaces.Any( i => i.SpecializationDepth == 0 )) );
                    foreach( var baseInterface in c.Interfaces.Where( i => !i.IsSpecialized ) )
                    {
                        InterfaceFamily? currentF = null;
                        var rootInterfaces = baseInterface.SpecializationDepth == 0
                                                ? new[] { baseInterface }
                                                : baseInterface.Interfaces.Where( i => i.SpecializationDepth == 0 );
                        foreach( var root in rootInterfaces )
                        {
                            if( families.TryGetValue( root, out var f ) )
                            {
                                if( currentF == null ) currentF = f;
                                else if( currentF != f )
                                {
                                    currentF.MergeWith( f );
                                    families[root] = currentF;
                                    m.Info( $"Family interfaces merged because of '{baseInterface.Type}'." );
                                    familiesHasBeenMerged = true;
                                }
                            }
                            else
                            {
                                currentF ??= new InterfaceFamily();
                                families.Add( root, currentF );
                            }
                            currentF._interfaces.AddRange( baseInterface.Interfaces );
                            currentF._interfaces.Add( baseInterface );
                        }
                        if( currentF != null )
                        {
                            if( !currentF._classes.ContainsKey( c ) )
                            {
                                currentF._classes.Add( c, new SCRClass( currentF, c ) );
                            }
                        }
                    }
                }
                IReadOnlyCollection<InterfaceFamily> result = families.Values;
                if( familiesHasBeenMerged ) result = result.Distinct().ToList();
                return result;
            }

            void MergeWith( InterfaceFamily f )
            {
                Debug.Assert( _interfaces.Intersect( f._interfaces ).Any() == false );
                _interfaces.UnionWith( f._interfaces );
                _classes.AddRange( f._classes );
            }

            public string BaseInterfacesToString()
            {
                return Interfaces.Where( i => !i.IsSpecialized ).Select( i => i.Type.ToCSharpName() ).Concatenate( "', '" );
            }

            public string RootInterfacesToString()
            {
                return Interfaces.Where( i => i.SpecializationDepth == 0 ).Select( i => i.Type.ToCSharpName() ).Concatenate( "', '" );
            }

            public override string ToString()
            {
                return $"'{RootInterfacesToString()}' family with {Interfaces.Count} interfaces on {Classes.Count} classes.";
            }
        }

        /// <summary>
        /// Called once Mutable items have been created.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="typeResult">The types discovery result.</param>
        /// <returns>True on success, false on error.</returns>
        bool ServiceFinalHandling( IActivityMonitor monitor, CKTypeCollectorResult typeResult )
        {
            var engineMap = typeResult.RealObjects.EngineMap;
            using( monitor.OpenInfo( $"Services final handling." ) )
            {
                try
                {
                    // Registering Interfaces: Families creation from all most specialized classes' supported interfaces.
                    var allClasses = typeResult.AutoServices.RootClasses
                                        .Concat( typeResult.AutoServices.SubGraphRootClasses )
                                        .Select( c => c.MostSpecialized! );
                    Debug.Assert( allClasses.GroupBy( c => c ).All( g => g.Count() == 1 ) );
                    IReadOnlyCollection<InterfaceFamily> families = InterfaceFamily.Build( monitor, allClasses );
                    if( families.Count == 0 )
                    {
                        monitor.Warn( "No IAuto Service interface found. Nothing can be mapped at the Service Interface level." );
                    }
                    else monitor.Trace( $"{families.Count} Service families found." );
                    bool success = true;
                    foreach( var f in families )
                    {
                        success &= f.Resolve( monitor );
                    }
                    if( success )
                    {
                        success &= FinalRegistration( monitor, typeResult.AutoServices, engineMap, typeResult.KindComputeFacade, families );
                    }
                    return success;
                }
                catch( Exception ex )
                {
                    monitor.Fatal( ex );
                    return false;
                }
            }

            static bool FinalRegistration( IActivityMonitor monitor,
                                           AutoServiceCollectorResult typeResult,
                                           StObjObjectEngineMap engineMap,
                                           IAutoServiceKindComputeFacade kindComputeFacade,
                                           IEnumerable<InterfaceFamily> families )
            {
                using( monitor.OpenInfo( "Final Service registration." ) )
                {
                    bool success = true;
                    foreach( var c in typeResult.RootClasses )
                    {
                        RegisterClassMapping( monitor, engineMap, kindComputeFacade, c, ref success );
                    }
                    foreach( var f in families )
                    {
                        Debug.Assert( f.Resolved != null );
                        foreach( var i in f.Interfaces )
                        {
                            RegisterMapping( monitor, engineMap, kindComputeFacade, i.Type, f.Resolved, ref success );
                        }
                    }
                    monitor.CloseGroup( $"Registered {engineMap.ObjectMappings.Count} real objects and {engineMap.Mappings.Count} auto services mappings." );
                    return success;
                }

                static void RegisterClassMapping( IActivityMonitor monitor,
                                                  StObjObjectEngineMap engineMap,
                                                  IAutoServiceKindComputeFacade kindComputeFacade,
                                                  AutoServiceClassInfo c,
                                                  ref bool success )
                {
                    if( !c.IsRealObject )
                    {
                        Debug.Assert( c.MostSpecialized != null );
                        RegisterMapping( monitor, engineMap, kindComputeFacade, c.ClassType, c.MostSpecialized, ref success );
                        foreach( var s in c.Specializations )
                        {
                            RegisterClassMapping( monitor, engineMap, kindComputeFacade, s, ref success );
                        }
                    }
                    else
                    {
                        monitor.Debug( $"Skipping '{c}' Service class mapping since it is a Real object." );
                    }
                }

                static void RegisterMapping( IActivityMonitor monitor,
                                             StObjObjectEngineMap engineMap,
                                             IAutoServiceKindComputeFacade kindComputeFacade,
                                             Type t,
                                             AutoServiceClassInfo final,
                                             ref bool success )
                {
                    final.ComputeFinalTypeKind( monitor, kindComputeFacade, new Stack<AutoServiceClassInfo>(), ref success );
                    if( success )
                    {
                        monitor.Debug( $"Map '{t}' -> '{final}'." );
                        if( final.IsRealObject )
                        {
                            engineMap.RegisterServiceFinalObjectMapping( t, final.TypeInfo );
                        }
                        else
                        {
                            engineMap.RegisterFinalSimpleMapping( t, final );
                        }
                        if( t != final.ClassType ) final.TypeInfo.AddUniqueMapping( t );
                    }
                }
            }

        }

    }
}
