using CK.CodeGen;
using CK.Core;
using CK.Setup;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

#nullable enable

namespace CK.Setup
{
    public partial class StObjCollectorResult
    {

        internal bool GenerateSourceCode( IActivityMonitor monitor,
                                          StObjEngineRunContext.GenBinPath g,
                                          string? informationalVersion,
                                          IEnumerable<ICSCodeGenerator> aspectsGenerators )
        {
            var secondPasses = new List<MultiPassCodeGeneration>();
            var allGenerators = new List<ICSCodeGeneratorWithFinalization>();
            if( !GenerateSourceCodeFirstPass( monitor, g, informationalVersion, aspectsGenerators, secondPasses, allGenerators ) )
            {
                return false;
            }
            var (success, runSignature) = GenerateSourceCodeSecondPass( monitor, g, secondPasses, allGenerators );
            if( success )
            {
                Throw.DebugAssert( g.ConfigurationGroup.RunSignature.IsZero || runSignature == g.ConfigurationGroup.RunSignature );
                g.ConfigurationGroup.RunSignature = runSignature;
            }
            return success;
        }

        bool GenerateSourceCodeFirstPass( IActivityMonitor monitor,
                                          ICSCodeGenerationContext codeGenContext,
                                          string? informationalVersion,
                                          IEnumerable<ICSCodeGenerator> aspectsGenerators,
                                          List<MultiPassCodeGeneration> secondPassCollector,
                                          List<ICSCodeGeneratorWithFinalization> finalGen )
        {
            Debug.Assert( EngineMap != null );
            Debug.Assert( codeGenContext.Assembly == _tempAssembly, "CodeGenerationContext mismatch." );
            try
            {
                Debug.Assert( _valueCollector != null );
                using( monitor.OpenInfo( $"Generating source code (first pass) for: {codeGenContext.CurrentRun.ConfigurationGroup.Names}." ) )
                using( monitor.CollectEntries( out var entries ) )
                {
                    using( monitor.OpenInfo( "Registering direct properties as PostBuildProperties." ) )
                    {
                        foreach( MutableItem item in EngineMap.StObjs.OrderedStObjs.Cast<MutableItem>() )
                        {
                            item.RegisterRemainingDirectPropertiesAsPostBuildProperties( _valueCollector );
                        }
                    }

                    // Retrieves CK._g workspace.
                    var ws = _tempAssembly.Code;
                    // Gets the global name space and starts with the informational version (if any),
                    // and, once for all, basic namespaces that we always want available.
                    var global = ws.Global;

                    if( !string.IsNullOrWhiteSpace( informationalVersion ) )
                    {
                        global.BeforeNamespace.Append( "[assembly:System.Reflection.AssemblyInformationalVersion(" )
                              .AppendSourceString( informationalVersion )
                              .Append( ")]" )
                              .NewLine();
                    }

                    // Injects setup assemblies into the workspace that will be used to generate source code.

                    // Injects System.Reflection (that may not be referenced by user code).
                    ws.EnsureAssemblyReference( typeof( BindingFlags ) );
                    // Injects the Microsoft.Extensions.DependencyInjection assembly (that may not be referenced by user code).
                    ws.EnsureAssemblyReference( typeof( Microsoft.Extensions.DependencyInjection.ServiceProvider ) );

                    // Model assemblies.
                    if( _typeResult.Assemblies.Count > 0 )
                    {
                        ws.EnsureAssemblyReference( _typeResult.Assemblies.Keys );
                    }
                    else
                    {
                        ws.EnsureAssemblyReference( typeof( StObjContextRoot ).Assembly );
                    }

                    // Injects, once for all, basic namespaces that we always want available into the global namespace.
                    global.GeneratedByComment().NewLine()
                          .EnsureUsing( "CK.Core" )
                          .EnsureUsing( "System" )
                          .EnsureUsing( "System.Collections.Generic" )
                          .EnsureUsing( "System.Collections.Immutable" )
                          .EnsureUsing( "System.Linq" )
                          .EnsureUsing( "System.Threading.Tasks" )
                          .EnsureUsing( "System.Text" )
                          .EnsureUsing( "System.Reflection" );

                    global.Append( "// We don't generate nullable enabled code and we disable all warnings." ).NewLine()
                          .Append( "#nullable disable" ).NewLine()
                          .Append( "#pragma warning disable" ).NewLine();

                    // Generates the Signature attribute implementation.
                    var nsStObj = global.FindOrCreateNamespace( "CK.StObj" );
                    nsStObj
                        .Append( "[AttributeUsage(AttributeTargets.Assembly)]" ).NewLine()
                        .Append( @"internal sealed class SignatureAttribute : Attribute" )
                        .OpenBlock()
                        .Append( "public SignatureAttribute( string s ) {}" ).NewLine()
                        .Append( "public readonly static (SHA1Value Signature, IReadOnlyList<string> Names) V = ( SHA1Value.Parse( (string)typeof( SignatureAttribute ).Assembly.GetCustomAttributesData().First( a => a.AttributeType == typeof( SignatureAttribute ) ).ConstructorArguments[0].Value )" ).NewLine()
                        .Append( ", " ).AppendArray( EngineMap.Names ).Append( " );" )
                        .CloseBlock();

                    // Generates the StObjContextRoot implementation.
                    CreateGeneratedRootContext( monitor, nsStObj, EngineMap.StObjs.OrderedStObjs );

                    using( monitor.OpenTrace( "Calls all ICSCodeGenerator items." ) )
                    {
                        int count = 0;
                        int count2ndPass = 0;
                        foreach( var g in EngineMap.AllTypesAttributesCache.Values.SelectMany( attr => attr.GetAllCustomAttributes<ICSCodeGenerator>() )
                                                   .Concat( aspectsGenerators ) )
                        {
                            if( g is ICSCodeGeneratorWithFinalization f ) finalGen.Add( f );
                            using( monitor.OpenTrace( $"ICSCodeGenerator n°{++count} - {g.GetType():C}." ) )
                            {
                                var second = MultiPassCodeGeneration.FirstPass( monitor, g, codeGenContext ).SecondPass;
                                if( second != null )
                                {
                                    ++count2ndPass;
                                    secondPassCollector.Add( second );
                                }
                            }
                        }
                        monitor.CloseGroup( $"{count} generator, {count2ndPass} second passes required." );
                    }

                    // Asks every ImplementableTypeInfo to generate their code. 
                    // This step MUST always be done, even if CompileOption is None and GenerateSourceFiles is false
                    // since during this step, side effects MAY occur (this is typically the case of the first run where
                    // the "reality cache" is created).
                    using( monitor.OpenTrace( "Calls all Type implementor items." ) )
                    {
                        int count = 0;
                        int count2ndPass = secondPassCollector.Count;
                        foreach( var t in _typeResult.TypesToImplement )
                        {
                            using( monitor.OpenTrace( $"Type implementor n°{count} - {t.GetType():C}." ) )
                            {
                                t.RunFirstPass( monitor, codeGenContext, secondPassCollector );
                            }
                        }
                        monitor.CloseGroup( $"{count} Type implementors, {secondPassCollector.Count - count2ndPass} second passes required." );
                    }
                    if( entries.Count != 0 )
                    {
                        using( monitor.OpenFatal( $"{entries.Count} error(s). Summary:" ) )
                        {
                            foreach( var e in entries )
                            {
                                monitor.Trace( $"{e.MaskedLevel} - {e.Text}" );
                            }
                        }
                        return false;
                    }
                }
                return true;
            }
            catch( Exception ex )
            {
                monitor.Error( "While generating final source code.", ex );
                return false;
            }
        }

        (bool Success, SHA1Value RunSignature) GenerateSourceCodeSecondPass( IActivityMonitor monitor,
                                                                             ICSCodeGenerationContext codeGenContext,
                                                                             List<MultiPassCodeGeneration> secondPass,
                                                                             List<ICSCodeGeneratorWithFinalization> finalGen )
        {
            Debug.Assert( EngineMap != null );
            Debug.Assert( codeGenContext.Assembly == _tempAssembly, "CodeGenerationContext mismatch." );
            var configurationGroup = codeGenContext.CurrentRun.ConfigurationGroup;
            var runSignature = configurationGroup.RunSignature;

            try
            {
                using( monitor.OpenInfo( $"Generating source code (second pass) for: {configurationGroup.Names}." ) )
                using( monitor.CollectEntries( out var entries ) )
                {
                    if( MultiPassCodeGeneration.RunSecondPass( monitor, codeGenContext, secondPass ) )
                    {
                        foreach( var g in finalGen )
                        {
                            if( !g.FinalImplement( monitor, codeGenContext ) )
                            {
                                // Ensure that an error is logged.
                                monitor.Error( $"Generator '{g.GetType():C}': FinalImplement method failed." );
                            }
                        }
                    }
                    else
                    {
                        // Ensure that an error is logged.
                        if( entries.Count == 0 )
                        {
                            monitor.Error( ActivityMonitor.Tags.ToBeInvestigated, "Second pass code generation failed but no errors have been logged." );
                        }
                    }
                    if( entries.Count != 0 )
                    {
                        using( monitor.OpenFatal( $"{entries.Count} error(s). Summary:" ) )
                        {
                            foreach( var e in entries )
                            {
                                monitor.Trace( $"{e.MaskedLevel} - {e.Text}" );
                            }
                        }
                        return (false, runSignature);
                    }
                }
                // Code generation itself succeeds.
                if( configurationGroup.IsUnifiedPure )
                {
                    Debug.Assert( runSignature.IsZero );
                    monitor.Info( "Purely unified group: source code generation skipped." );
                    return (true, runSignature);
                }
                if( !codeGenContext.SaveSource
                    && codeGenContext.CompileOption == CompileOption.None
                    && !runSignature.IsZero )
                {
                    monitor.Info( "Configured GenerateSourceFile is false, CompileOption is None and SHA1 is known: nothing more to do." );
                    return (true, runSignature);
                }
                ICodeWorkspace? ws = codeGenContext.Assembly.Code;

                // Trick to avoid allocating the big string code more than once: the hash is first computed on the source
                // without the signature header, a part is injected at the top of the file (before anything else) and
                // the final big string is built only once.
                if( runSignature.IsZero )
                {
                    using( var s = new HashStream( System.Security.Cryptography.HashAlgorithmName.SHA1 ) )
                    using( var w = new StreamWriter( s ) )
                    {
                        ws.WriteGlobalSource( w );
                        w.Flush();
                        runSignature = new SHA1Value( s.GetFinalResult().AsSpan() );
                    }
                    monitor.Info( $"Computed file signature: {runSignature}." );
                }
                else
                {
                    monitor.Info( $"Using provided file signature: {runSignature}." );
                }

                // Injects the SHA1 signature at the top.
                ws.Global.BeforeNamespace.CreatePart( top: true ).Append( @"[assembly: CK.StObj.Signature( " )
                                                                 .AppendSourceString( runSignature.ToString() )
                                                                 .Append( " )]" );

                // The source code is available if SaveSource is true.
                string? code = null;

                // If asked to do so, we always save it, even if parsing or compilation fails
                // but only if the current G0.cs has not the same signature: this preserves any
                // manual modifications to the file so that code generation can be perfected before
                // being integrated back into the engine code.
                if( configurationGroup.SaveSource
                    && configurationGroup.GeneratedSource.GetSignature( monitor ) != runSignature )
                {
                    code = ws.GetGlobalSource();
                    configurationGroup.GeneratedSource.CreateOrUpdate( monitor, code );
                }
                if( codeGenContext.CompileOption == CompileOption.None )
                {
                    monitor.Info( "Configured CompileOption is None: nothing more to do." );
                    return (true, runSignature);
                }
                code ??= ws.GetGlobalSource();
                Debug.Assert( code != null );

                var targetPath = codeGenContext.CompileOption == CompileOption.Compile
                                                            ? configurationGroup.GeneratedAssembly.Path
                                                            : default;
                using( monitor.OpenInfo( codeGenContext.CompileOption == CompileOption.Compile
                                            ? $"Compiling source code (using C# v10.0 language version) and saving to '{targetPath}'."
                                            : "Only parsing source code, using C# v10.0 language version (skipping compilation)." ) )
                {
                    var result = CodeGenerator.Generate( code,
                                                         targetPath.IsEmptyPath ? null : targetPath,
                                                         ws.AssemblyReferences,
                                                         new CSharpParseOptions( LanguageVersion.CSharp10 ) );

                    result.LogResult( monitor );
                    if( result.Success )
                    {
                        if( codeGenContext.CompileOption == CompileOption.Compile )
                        {
                            configurationGroup.GeneratedAssembly.CreateOrUpdateSignatureFile( runSignature );
                        }
                    }
                    else
                    {
                        monitor.Debug( code );
                        monitor.CloseGroup( "Failed" );
                    }
                    return (result.Success, runSignature);
                }

            }
            catch( Exception ex )
            {
                monitor.Error( "While generating final source code.", ex );
                return (false, runSignature);
            }
        }

        const string _gStObj = """
            class GRealObject : IStObj
            {
                public GRealObject( Type t, IStObj g, int idx )
                {
                    ClassType = t;
                    Generalization = g;
                    IndexOrdered = idx;
                }

                public Type ClassType { get; }

                public IStObj Generalization { get; }

                public IStObj Specialization { get; internal set; }

                public IStObjFinalImplementation FinalImplementation { get; internal set; }

                public int IndexOrdered { get; }

                internal StObjMapping AsMapping => new StObjMapping( this, FinalImplementation );
            }
            """;
        const string _gFinalStObj = """
            sealed class GFinalRealObject : GRealObject, IStObjFinalImplementation
            {
                public GFinalRealObject( object impl, Type actualType, IReadOnlyCollection<Type> mult, IReadOnlyCollection<Type> uniq, Type t, IStObj g, int idx )
                        : base( t, g, idx )
                {
                    FinalImplementation = this;
                    Implementation = impl;
                    MultipleMappings = mult;
                    UniqueMappings = uniq;
                }

                public object Implementation { get; }

                public Type FinalType => ClassType;

                public bool IsScoped => false;

                public IReadOnlyCollection<Type> MultipleMappings { get; }

                public IReadOnlyCollection<Type> UniqueMappings { get; }
            }
            """;
        const string _gMultiple = """
            sealed class GMultiple : IStObjMultipleInterface
            {
                public GMultiple( bool s, Type i, Type e, IStObjFinalClass[] im )
                {
                    IsScoped = s;
                    ItemType = i;
                    EnumerableType = e;
                    Implementations = im;
                }

                public bool IsScoped { get; }

                public Type ItemType { get; }

                public Type EnumerableType { get; }

                public IReadOnlyCollection<IStObjFinalClass> Implementations { get; }
            }
            """;

        void CreateGeneratedRootContext( IActivityMonitor monitor, INamespaceScope ns, IReadOnlyList<IStObjResult> orderedStObjs )
        {
            Debug.Assert( EngineMap != null );

            using var rootRegion = ns.Region();
            ns.Append( _gStObj ).NewLine()
              .Append( _gFinalStObj ).NewLine()
              .Append( _gMultiple ).NewLine();

            var rootType = ns.CreateType( "sealed class " + StObjContextRoot.RootContextTypeName + " : IStObjMap, IStObjObjectMap, IStObjServiceMap" );

            rootType.Append( "static readonly GRealObject[] _stObjs;" ).NewLine()
                    .Append( "static readonly GFinalRealObject[] _finalStObjs;" ).NewLine()
                    .Append( "static readonly Dictionary<Type,GFinalRealObject> _map;" ).NewLine()
                    .Append( "static readonly IReadOnlyCollection<VFeature> _vFeatures;" ).NewLine()
                    .Append( "static int _intializeOnce;" ).NewLine();

            rootType.Append( @"
public IStObjObjectMap StObjs => this;

IReadOnlyList<string> IStObjMap.Names => CK.StObj.SignatureAttribute.V.Names;
SHA1Value IStObjMap.GeneratedSignature => CK.StObj.SignatureAttribute.V.Signature;
IReadOnlyCollection<VFeature> IStObjMap.Features => _vFeatures;

IStObjFinalImplementation? IStObjObjectMap.ToLeaf( Type t ) => ToRealObjectLeaf( t );
object? IStObjObjectMap.Obtain( Type t ) => _map.TryGetValue( t, out var s ) ? s.Implementation : null;
IReadOnlyList<IStObjFinalImplementation> IStObjObjectMap.FinalImplementations => _finalStObjs;
IEnumerable<StObjMapping> IStObjObjectMap.StObjs => _stObjs.Select( s => s.AsMapping );

[Obsolete( ""Now that StObj are static, there is no need to rely on the DI and cast into GeneratedRootContext. Simply use CK.StObj.GeneratedRootContext.RealObjects."", true )]
internal IReadOnlyList<IStObj> InternalRealObjects => _stObjs;
[Obsolete( ""Now that StObj are static, there is no need to rely on the DI and cast into GeneratedRootContext. Simply use CK.StObj.GeneratedRootContext.FinalRealObjects."", true )]
internal IReadOnlyList<IStObjFinalImplementation> InternalFinalRealObjects => _finalStObjs;

// Direct static access to the RealObjects and the FinalRealObjects for generated code.
public static IReadOnlyList<IStObj> RealObjects => _stObjs;
public static IReadOnlyList<IStObjFinalImplementation> FinalRealObjects => _finalStObjs;
public static IStObjFinalImplementation? ToRealObjectLeaf( Type t ) => _map.TryGetValue( t, out var s ) ? s : null;
" );
            var rootStaticCtor = rootType.CreateFunction( $"static {StObjContextRoot.RootContextTypeName}()" );
            SetupObjectsGraph( rootStaticCtor, orderedStObjs, _typeResult.RealObjects.EngineMap );

            // Construct and Initialize methods takes a monitor and the context instance: we need the instance constructor.
            // We ensure that this StObjMap can be initialized once and only once (static bool _intializeOnce).
            // This doesn't mean that this StObjMap can be registered in a ServiceCollection only once: services registration
            // (including endpoints) are on the "run" side (the EndpointType<TScopedData> is a pure service that manages the 
            // services as opposed to the real object EndpointDefinition).
            GenerateInstanceConstructor( rootType, orderedStObjs );

            // Ignores null (error) return here: we always generate the code.
            // Errors are detected through the monitor by the caller.
            var hostedServiceLifetimeTrigger = HostedServiceLifetimeTriggerImpl.DiscoverMethods( monitor, EngineMap );
            hostedServiceLifetimeTrigger?.GenerateHostedServiceLifetimeTrigger( monitor, EngineMap, rootType );

            var serviceGen = new ServiceSupportCodeGenerator( rootType, rootStaticCtor );
            serviceGen.CreateServiceSupportCode( EngineMap );
            serviceGen.CreateRealObjectConfigureServiceMethod( orderedStObjs );
            serviceGen.CreateConfigureServiceMethod( monitor, EngineMap );

            GenerateVFeatures( monitor, rootStaticCtor, rootType, EngineMap.Features );
        }

        static void SetupObjectsGraph( IFunctionScope rootStaticCtor, IReadOnlyList<IStObjResult> orderedStObjs, StObjObjectEngineMap map )
        {
            using var region = rootStaticCtor.Region();
            rootStaticCtor.NewLine()
                          .Append( $"_stObjs = new GRealObject[{orderedStObjs.Count}];" ).NewLine()
                          .Append( $"_finalStObjs = new GFinalRealObject[{map.FinalImplementations.Count}];" ).NewLine()
                          .Append( $"_map = new Dictionary<Type,GFinalRealObject>();" ).NewLine();
            int iStObj = 0;
            int iImplStObj = 0;
            foreach( var m in orderedStObjs )
            {
                Debug.Assert( (m.Specialization != null) == (m != m.FinalImplementation) );
                string generalization = m.Generalization == null ? "null" : $"_stObjs[{m.Generalization.IndexOrdered}]";
                rootStaticCtor.Append( $"_stObjs[{iStObj++}] = " );
                if( m.Specialization == null )
                {
                    rootStaticCtor.Append( "_finalStObjs[" ).Append( iImplStObj++ ).Append( "] = new GFinalRealObject( new " )
                                  .AppendCSharpName( m.FinalImplementation.FinalType, true, true, true ).Append( "(), " )
                                  .AppendTypeOf( m.FinalImplementation.FinalType ).Append( ", " ).NewLine()
                                  .AppendArray( m.FinalImplementation.MultipleMappings ).Append( ", " ).NewLine()
                                  .AppendArray( m.FinalImplementation.UniqueMappings ).Append( ", " ).NewLine();
                }
                else rootStaticCtor.Append( "new GRealObject(" );

                rootStaticCtor.AppendTypeOf( m.ClassType ).Append( ", " )
                              .Append( generalization )
                              .Append( ", " ).Append( m.IndexOrdered ).Append( " );" ).NewLine();

            }

            IReadOnlyDictionary<object, MutableItem> allMappings = map.RawMappings;
            // We skip highest implementation Type mappings (ie. RealObjectInterfaceKey keys) since 
            // there is no ToHead mapping (to root generalization) on final (runtime) IStObjMap.
            foreach( var e in allMappings.Where( e => e.Key is Type ) )
            {
                rootStaticCtor.Append( $"_map.Add( " ).AppendTypeOf( (Type)e.Key )
                              .Append( ", (GFinalRealObject)_stObjs[" ).Append( e.Value.IndexOrdered ).Append( "] );" ).NewLine();
            }
            if( orderedStObjs.Count > 0 )
            {
                rootStaticCtor.Append( $"int iStObj = {orderedStObjs.Count};" ).NewLine()
                              .Append( "while( --iStObj >= 0 ) {" ).NewLine()
                              .Append( " var o = _stObjs[iStObj];" ).NewLine()
                              .Append( " if( o.Specialization == null ) {" ).NewLine()
                              .Append( "  var oF = (GFinalRealObject)o;" ).NewLine()
                              .Append( "  GRealObject g = (GRealObject)o.Generalization;" ).NewLine()
                              .Append( "  while( g != null ) {" ).NewLine()
                              .Append( "   g.Specialization = o;" ).NewLine()
                              .Append( "   g.FinalImplementation = oF;" ).NewLine()
                              .Append( "   o = g;" ).NewLine()
                              .Append( "   g = (GRealObject)o.Generalization;" ).NewLine()
                              .Append( "  }" ).NewLine()
                              .Append( " }" ).NewLine()
                              .Append( "}" ).NewLine();
            }
        }

        static void GenerateInstanceConstructor( ITypeScope rootType, IReadOnlyList<IStObjResult> orderedStObjs )
        {
            using var region = rootType.Region();
            var rootCtor = rootType.NewLine()
                           .CreateFunction( $"public {StObjContextRoot.RootContextTypeName}( IActivityMonitor monitor )" );

            rootCtor.Append( @"if( System.Threading.Interlocked.CompareExchange( ref _intializeOnce, 1, 0 ) != 0 )" )
                    .OpenBlock()
                    .Append( "monitor.UnfilteredLog( LogLevel.Warn, null, \"This context has already been initialized.\", null );" )
                    .Append( "return;" )
                    .CloseBlock();

            InitializePreConstructPropertiesAndCallConstruct( rootCtor, orderedStObjs );
            InitializePostBuildProperties( rootCtor, orderedStObjs );
            CallInitializeMethods( rootCtor, orderedStObjs );
        }

        static void InitializePreConstructPropertiesAndCallConstruct( IFunctionScope rootCtor, IReadOnlyList<IStObjResult> orderedStObjs )
        {
            using var region = rootCtor.Region();
            var propertyCache = new Dictionary<ValueTuple<Type, string>, string>();
            foreach( MutableItem m in orderedStObjs.Cast<MutableItem>() )
            {
                if( m.PreConstructProperties != null )
                {
                    foreach( var setter in m.PreConstructProperties )
                    {
                        Debug.Assert( setter.Property.DeclaringType != null );
                        Type decl = setter.Property.DeclaringType;
                        var key = ValueTuple.Create( decl, setter.Property.Name );
                        if( !propertyCache.TryGetValue( key, out string? varName ) )
                        {
                            varName = "pI" + propertyCache.Count.ToString();
                            rootCtor
                                .Append( "PropertyInfo " )
                                .Append( varName )
                                .Append( "=" )
                                .AppendTypeOf( decl )
                                .Append( ".GetProperty(" )
                                .AppendSourceString( setter.Property.Name )
                                .Append( ",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);" )
                                .NewLine();
                            propertyCache.Add( key, varName );

                        }
                        rootCtor.Append( varName )
                               .Append( ".SetValue(_stObjs[" )
                               .Append( m.IndexOrdered ).Append( "].FinalImplementation.Implementation," );
                        GenerateValue( rootCtor, setter.Value );
                        rootCtor.Append( ");" ).NewLine();
                    }
                }
                if( m.ConstructParametersAboveRoot != null )
                {
                    foreach( var mp in m.ConstructParametersAboveRoot )
                    {
                        Debug.Assert( mp.Item2.Count > 0 );
                        rootCtor.AppendTypeOf( mp.Item1 );
                        CallConstructMethod( rootCtor, m, mp.Item2 );
                    }
                }
                if( m.RealObjectType.StObjConstruct != null )
                {
                    Debug.Assert( m.ConstructParameters.Count > 0 );
                    rootCtor.Append( $"_stObjs[{m.IndexOrdered}].ClassType" );
                    CallConstructMethod( rootCtor, m, m.ConstructParameters );
                }
            }

            static void CallConstructMethod( IFunctionScope rootCtor, MutableItem m, IEnumerable<IStObjMutableParameter> parameters )
            {
                rootCtor.Append( ".GetMethod(" )
                        .AppendSourceString( StObjContextRoot.ConstructMethodName )
                        .Append( ", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly )" )
                        .Append( $".Invoke( _stObjs[{m.IndexOrdered}].FinalImplementation.Implementation, new object[] {{" );
                // Missing Value {get;} on IStObjMutableParameter and we need the BuilderValueIndex...
                // Hideous downcast for the moment.
                foreach( var p in parameters.Cast<MutableParameter>() )
                {
                    if( p.BuilderValueIndex < 0 )
                    {
                        rootCtor.Append( $"_stObjs[{-(p.BuilderValueIndex + 1)}].FinalImplementation.Implementation" );
                    }
                    else GenerateValue( rootCtor, p.Value );
                    rootCtor.Append( ',' );
                }
                rootCtor.Append( "});" ).NewLine();
            }

        }

        static void InitializePostBuildProperties( IFunctionScope rootCtor, IReadOnlyList<IStObjResult> orderedStObjs )
        {
            using var region = rootCtor.Region();
            foreach( MutableItem m in orderedStObjs.Cast<MutableItem>() )
            {
                if( m.PostBuildProperties != null )
                {
                    foreach( var p in m.PostBuildProperties )
                    {
                        Debug.Assert( p.Property.DeclaringType != null );
                        Type decl = p.Property.DeclaringType;
                        rootCtor.AppendTypeOf( decl )
                               .Append( $".GetProperty( \"{p.Property.Name}\", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic )" )
                               .Append( $".SetValue(_stObjs[{m.IndexOrdered}].FinalImplementation.Implementation, " );
                        GenerateValue( rootCtor, p.Value );
                        rootCtor.Append( ");" ).NewLine();
                    }
                }
            }
        }

        static void CallInitializeMethods( IFunctionScope rootCtor, IReadOnlyList<IStObjResult> orderedStObjs )
        {
            using var region = rootCtor.Region();
            foreach( MutableItem m in orderedStObjs.Cast<MutableItem>() )
            {
                foreach( MethodInfo init in m.RealObjectType.AllStObjInitialize )
                {
                    if( init == m.RealObjectType.StObjInitialize ) rootCtor.Append( $"_stObjs[{m.IndexOrdered}].ClassType" );
                    else rootCtor.AppendTypeOf( init.DeclaringType! );

                    rootCtor.Append( ".GetMethod(" )
                            .AppendSourceString( StObjContextRoot.InitializeMethodName )
                            .Append( ", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly )" )
                            .NewLine();
                    rootCtor.Append( $".Invoke( _stObjs[{m.IndexOrdered}].FinalImplementation.Implementation, new object[]{{ monitor, this }} );" )
                            .NewLine();
                }
            }
        }

        static void GenerateVFeatures( IActivityMonitor monitor, IFunctionScope rootStaticCtor, ITypeScope rootType, IReadOnlyCollection<VFeature> features )
        {
            using var region = rootStaticCtor.Region();
            monitor.Info( $"Generating VFeatures: {features.Select( f => f.ToString()).Concatenate()}." );

            rootStaticCtor.Append( "_vFeatures = new VFeature[]{ " );
            bool atLeastOne = false;
            foreach( var f in features )
            {
                if( atLeastOne ) rootStaticCtor.Append( ", " );
                atLeastOne = true;
                rootStaticCtor.Append( "new VFeature( " )
                        .AppendSourceString( f.Name )
                        .Append(',')
                        .Append( "CSemVer.SVersion.Parse( " )
                        .AppendSourceString( f.Version.ToNormalizedString() )
                        .Append( " ) )" );
            }
            rootStaticCtor.Append( "};" );
        }

        static void GenerateValue( ICodeWriter b, object? o )
        {
            if( o is IActivityMonitor )
            {
                b.Append( "monitor" );
            }
            else if( o is MutableItem item )
            {
                b.Append( $"_stObjs[{item.IndexOrdered}].FinalImplementation.Implementation" );
            }
            else
            {
                b.Append( o );
            }
        }
    }
}




