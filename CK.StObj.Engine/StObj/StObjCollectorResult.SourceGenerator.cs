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
            List<MultiPassCodeGeneration> secondPasses = new List<MultiPassCodeGeneration>();
            if( !GenerateSourceCodeFirstPass( monitor, g, informationalVersion, secondPasses, aspectsGenerators ) )
            {
                return false;
            }
            var (success, runSignature) = GenerateSourceCodeSecondPass( monitor, g, secondPasses );
            if( success )
            {
                Debug.Assert( g.ConfigurationGroup.RunSignature.IsZero || runSignature == g.ConfigurationGroup.RunSignature );
                g.ConfigurationGroup.RunSignature = runSignature;
            }
            return success;
        }

        bool GenerateSourceCodeFirstPass( IActivityMonitor monitor,
                                          ICSCodeGenerationContext codeGenContext,
                                          string? informationalVersion,
                                          List<MultiPassCodeGeneration> collector,
                                          IEnumerable<ICSCodeGenerator> aspectsGenerators )
        {
            Debug.Assert( EngineMap != null );
            Debug.Assert( codeGenContext.Assembly == _tempAssembly, "CodeGenerationContext mismatch." );
            try
            {
                Debug.Assert( _valueCollector != null );
                using( monitor.OpenInfo( $"Generating source code (first pass) for: {codeGenContext.CurrentRun.ConfigurationGroup.Names}." ) )
                using( monitor.CollectEntries( out var errorSummary ) )
                {
                    using( monitor.OpenInfo( "Registering direct properties as PostBuildProperties." ) )
                    {
                        foreach( MutableItem item in EngineMap.StObjs.OrderedStObjs )
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

                    // Injects System.Reflection and setup assemblies into the
                    // workspace that will be used to generate source code.
                    ws.EnsureAssemblyReference( typeof( BindingFlags ) );
                    if( CKTypeResult.Assemblies.Count > 0 )
                    {
                        ws.EnsureAssemblyReference( CKTypeResult.Assemblies );
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
                    GenerateStObjContextRootSource( monitor, nsStObj, EngineMap.StObjs.OrderedStObjs );

                    // Calls all ICSCodeGenerator items.
                    foreach( var g in EngineMap.AllTypesAttributesCache.Values.SelectMany( attr => attr.GetAllCustomAttributes<ICSCodeGenerator>() )
                                               .Concat( aspectsGenerators ) )
                    {
                        var second = MultiPassCodeGeneration.FirstPass( monitor, g, codeGenContext ).SecondPass;
                        if( second != null ) collector.Add( second );
                    }

                    // Asks every ImplementableTypeInfo to generate their code. 
                    // This step MUST always be done, even if CompileOption is None and GenerateSourceFiles is false
                    // since during this step, side effects MAY occur (this is typically the case of the first run where
                    // the "reality cache" is created).
                    foreach( var t in CKTypeResult.TypesToImplement )
                    {
                        t.RunFirstPass( monitor, codeGenContext, collector );
                    }

                    if( errorSummary.Count > 0 )
                    {
                        using( monitor.OpenFatal( $"{errorSummary.Count} error(s). Summary:" ) )
                        {
                            foreach( var e in errorSummary )
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
                                                                             List<MultiPassCodeGeneration> secondPass )
        {
            Debug.Assert( EngineMap != null );
            Debug.Assert( codeGenContext.Assembly == _tempAssembly, "CodeGenerationContext mismatch." );
            var configurationGroup = codeGenContext.CurrentRun.ConfigurationGroup;
            var runSignature = configurationGroup.RunSignature;

            try
            {
                using( monitor.OpenInfo( $"Generating source code (second pass) for: {configurationGroup.Names}." ) )
                using( monitor.CollectEntries( out var errorSummary ) )
                {
                    MultiPassCodeGeneration.RunSecondPass( monitor, codeGenContext, secondPass );
                    if( errorSummary.Count > 0 )
                    {
                        using( monitor.OpenFatal( $"{errorSummary.Count} error(s). Summary:" ) )
                        {
                            foreach( var e in errorSummary )
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

        const string _sourceGStObj = @"
class GStObj : IStObj
{
    public GStObj( Type t, IStObj g, int idx )
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
}";
        const string _sourceFinalGStObj = @"
sealed class GFinalStObj : GStObj, IStObjFinalImplementation
{
    public GFinalStObj( object impl, Type actualType, IReadOnlyCollection<Type> mult, IReadOnlyCollection<Type> uniq, Type t, IStObj g, int idx )
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
}";
        void GenerateStObjContextRootSource( IActivityMonitor monitor, INamespaceScope ns, IReadOnlyList<IStObjResult> orderedStObjs )
        {
            Debug.Assert( EngineMap != null );

            ns.GeneratedByComment().NewLine()
              .Append( _sourceGStObj ).NewLine()
              .Append( _sourceFinalGStObj ).NewLine();

            var rootType = ns.CreateType( "sealed class " + StObjContextRoot.RootContextTypeName + " : IStObjMap, IStObjObjectMap, IStObjServiceMap" );

            rootType.Append( "static readonly GStObj[] _stObjs;" ).NewLine()
                    .Append( "static readonly GFinalStObj[] _finalStObjs;" ).NewLine()
                    .Append( "static readonly Dictionary<Type,GFinalStObj> _map;" ).NewLine()
                    .Append( "static readonly IReadOnlyCollection<VFeature> _vFeatures;" ).NewLine()
                    .Append( "static int _intializeOnce;" ).NewLine();

            rootType.Append( @"
public IStObjObjectMap StObjs => this;

IReadOnlyList<string> IStObjMap.Names => CK.StObj.SignatureAttribute.V.Names;
SHA1Value IStObjMap.GeneratedSignature => CK.StObj.SignatureAttribute.V.Signature;
IReadOnlyCollection<VFeature> IStObjMap.Features => _vFeatures;

IStObjFinalImplementation? IStObjObjectMap.ToLeaf( Type t ) => GToLeaf( t );
object? IStObjObjectMap.Obtain( Type t ) => _map.TryGetValue( t, out var s ) ? s.Implementation : null;
IReadOnlyList<IStObjFinalImplementation> IStObjObjectMap.FinalImplementations => _finalStObjs;
IEnumerable<StObjMapping> IStObjObjectMap.StObjs => _stObjs.Select( s => s.AsMapping );

GFinalStObj? GToLeaf( Type t ) => _map.TryGetValue( t, out var s ) ? s : null;

[Obsolete( ""Now that StObj are static, there is no need to rely on the DI and cast into GeneratedRootContext. Simply use CK.StObj.GeneratedRootContext.GenStObjs."", true )]
internal IReadOnlyList<IStObj> InternalRealObjects => _stObjs;
[Obsolete( ""Now that StObj are static, there is no need to rely on the DI and cast into GeneratedRootContext. Simply use CK.StObj.GeneratedRootContext.GenFinalStObjs."", true )]
internal IReadOnlyList<IStObjFinalImplementation> InternalFinalRealObjects => _finalStObjs;

// Direct static access to the StObjs and the FinalStObjs for generated code.
public static IReadOnlyList<IStObj> GenStObjs => _stObjs;
public static IReadOnlyList<IStObjFinalImplementation> GenFinalStObjs => _finalStObjs;
" );
            var rootStaticCtor = rootType.GeneratedByComment().NewLine()
                                         .CreateFunction( $"static {StObjContextRoot.RootContextTypeName}()" );
            SetupObjectsGraph( rootStaticCtor, orderedStObjs, CKTypeResult.RealObjects.EngineMap );

            // Construct and Initialize methods takes a monitor and the context instance: we need the instance constructor.
            GenerateInstanceConstructor( rootType, orderedStObjs );

            // Ignores null (error) return here: we always generate the code.
            // Errors are detected through the monitor by the caller.
            var hostedServiceLifetimeTrigger = HostedServiceLifetimeTriggerImpl.DiscoverMethods( monitor, EngineMap );
            hostedServiceLifetimeTrigger?.GenerateHostedServiceLifetimeTrigger( monitor, EngineMap, rootType );

            var serviceGen = new ServiceSupportCodeGenerator( rootType, rootStaticCtor );
            serviceGen.CreateServiceSupportCode( EngineMap.Services );
            serviceGen.CreateRealObjectConfigureServiceMethod( orderedStObjs );
            serviceGen.CreateConfigureServiceMethod( monitor, EngineMap );

            GenerateVFeatures( monitor, rootStaticCtor, rootType, EngineMap.Features );
        }

        static void SetupObjectsGraph( IFunctionScope rootStaticCtor, IReadOnlyList<IStObjResult> orderedStObjs, StObjObjectEngineMap map )
        {
            rootStaticCtor.GeneratedByComment().NewLine()
                          .Append( $"_stObjs = new GStObj[{orderedStObjs.Count}];" ).NewLine()
                          .Append( $"_finalStObjs = new GFinalStObj[{map.FinalImplementations.Count}];" ).NewLine()
                          .Append( $"_map = new Dictionary<Type,GFinalStObj>();" ).NewLine();
            int iStObj = 0;
            int iImplStObj = 0;
            foreach( var m in orderedStObjs )
            {
                Debug.Assert( (m.Specialization != null) == (m != m.FinalImplementation) );
                string generalization = m.Generalization == null ? "null" : $"_stObjs[{m.Generalization.IndexOrdered}]";
                rootStaticCtor.Append( $"_stObjs[{iStObj++}] = " );
                if( m.Specialization == null )
                {
                    rootStaticCtor.Append( "_finalStObjs[" ).Append( iImplStObj++ ).Append( "] = new GFinalStObj( new " )
                                  .AppendCSharpName( m.FinalImplementation.FinalType, true, true, true ).Append( "(), " )
                                  .AppendTypeOf( m.FinalImplementation.FinalType ).Append( ", " ).NewLine()
                                  .AppendArray( m.FinalImplementation.MultipleMappings ).Append( ", " ).NewLine()
                                  .AppendArray( m.FinalImplementation.UniqueMappings ).Append( ", " ).NewLine();
                }
                else rootStaticCtor.Append( "new GStObj(" );

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
                              .Append( ", (GFinalStObj)_stObjs[" ).Append( e.Value.IndexOrdered ).Append( "] );" ).NewLine();
            }
            if( orderedStObjs.Count > 0 )
            {
                rootStaticCtor.Append( $"int iStObj = {orderedStObjs.Count};" ).NewLine()
                              .Append( "while( --iStObj >= 0 ) {" ).NewLine()
                              .Append( " var o = _stObjs[iStObj];" ).NewLine()
                              .Append( " if( o.Specialization == null ) {" ).NewLine()
                              .Append( "  var oF = (GFinalStObj)o;" ).NewLine()
                              .Append( "  GStObj g = (GStObj)o.Generalization;" ).NewLine()
                              .Append( "  while( g != null ) {" ).NewLine()
                              .Append( "   g.Specialization = o;" ).NewLine()
                              .Append( "   g.FinalImplementation = oF;" ).NewLine()
                              .Append( "   o = g;" ).NewLine()
                              .Append( "   g = (GStObj)o.Generalization;" ).NewLine()
                              .Append( "  }" ).NewLine()
                              .Append( " }" ).NewLine()
                              .Append( "}" ).NewLine();
            }
        }

        static void GenerateInstanceConstructor( ITypeScope rootType, IReadOnlyList<IStObjResult> orderedStObjs )
        {
            var rootCtor = rootType.GeneratedByComment().NewLine()
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
            rootCtor.GeneratedByComment().NewLine();
            var propertyCache = new Dictionary<ValueTuple<Type, string>, string>();
            foreach( MutableItem m in orderedStObjs )
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
            rootCtor.GeneratedByComment().NewLine();
            foreach( MutableItem m in orderedStObjs )
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
            rootCtor.GeneratedByComment().NewLine();
            foreach( MutableItem m in orderedStObjs )
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
            monitor.Info( $"Generating VFeatures: {features.Select( f => f.ToString()).Concatenate()}." );

            rootStaticCtor.GeneratedByComment().NewLine()
                          .Append( "_vFeatures = new VFeature[]{ " );
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




