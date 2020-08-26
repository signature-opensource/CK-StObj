using CK.CodeGen;
using CK.Core;
using CK.Setup;
using CK.Text;
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
        /// <summary>
        /// Captures code generation result.
        /// The default values is a failed result.
        /// </summary>
        public readonly struct CodeGenerateResult
        {
            readonly IReadOnlyList<string> _fileNames;

            /// <summary>
            /// Gets whether the generation succeeded.
            /// </summary>
            public readonly bool Success;

            /// <summary>
            /// Gets the generated file signature.
            /// Null whenever success is false.
            /// This can be non null when Success is false if an error occurred
            /// during the parse or compilation of the source files.
            /// </summary>
            public readonly SHA1Value? GeneratedSignature;

            /// <summary>
            /// Gets the list of files that have been generated: the assembly itself and
            /// any source code or other files.
            /// </summary>
            public IReadOnlyList<string> GeneratedFileNames => _fileNames ?? Array.Empty<string>();

            internal CodeGenerateResult( bool success, IReadOnlyList<string> fileNames, SHA1Value? s = null )
            {
                Success = success;
                _fileNames = fileNames;
                GeneratedSignature = s;
            }
        }

        /// <summary>
        /// Executes the first pass of code generation. This must be called on all <see cref="ICodeGenerationContext.AllBinPaths"/>, starting with
        /// the <see cref="ICodeGenerationContext.UnifiedBinPath"/>, before finalizing code generation by calling <see cref="GenerateSourceCodeSecondPass"/>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="codeGenContext">The code generation context that must be the one of this result.</param>
        /// <param name="informationalVersion">Optional informational version attribute content.</param>
        /// <param name="collector">The collector for second pass actions (for this <paramref name="codeGenContext"/>).</param>
        /// <returns>True on success, false on error.</returns>
        public bool GenerateSourceCodeFirstPass( IActivityMonitor monitor, ICodeGenerationContext codeGenContext, string? informationalVersion, List<MultiPassCodeGeneration> collector )
        {
            if( EngineMap == null ) throw new InvalidOperationException( nameof( HasFatalError ) );
            if( codeGenContext.Assembly != _tempAssembly ) throw new ArgumentException( "CodeGenerationContext mismatch.", nameof( codeGenContext ) );
            try
            {
                Debug.Assert( _valueCollector != null );
                IReadOnlyList<ActivityMonitorSimpleCollector.Entry>? errorSummary = null;
                using( monitor.OpenInfo( $"Generating source code (first pass) for: {codeGenContext.CurrentRun.Names}." ) )
                using( monitor.CollectEntries( entries => errorSummary = entries ) )
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
                    // Gets the global name space and starst with the informational version (if any),
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
                    global.EnsureUsing( "CK.Core" )
                          .EnsureUsing( "System" )
                          .EnsureUsing( "System.Collections.Generic" )
                          .EnsureUsing( "System.Linq" )
                          .EnsureUsing( "System.Threading.Tasks" )
                          .EnsureUsing( "System.Text" )
                          .EnsureUsing( "System.Reflection" );

                    // We don't generate nullable enabled code.
                    global.Append( "#nullable disable" ).NewLine();

                    // Generates the Signature attribute implementation.
                    var nsStObj = global.FindOrCreateNamespace( "CK.StObj" );
                    nsStObj.Append( @"internal class SignatureAttribute : Attribute" )
                        .OpenBlock()
                        .Append( "public SignatureAttribute( string s ) {}" ).NewLine()
                        .Append( "public readonly static (SHA1Value Signature, IReadOnlyList<string> Names) V = ( SHA1Value.Parse( (string)typeof( SignatureAttribute ).Assembly.GetCustomAttributesData().First( a => a.AttributeType == typeof( SignatureAttribute ) ).ConstructorArguments[0].Value )" ).NewLine()
                        .Append( ", " ).AppendArray( EngineMap.Names ).Append( " );" )
                        .CloseBlock();

                    // Generates the StObjContextRoot implementation.
                    GenerateStObjContextRootSource( monitor, nsStObj, EngineMap.StObjs.OrderedStObjs );

                    // Calls all ICodeGenerator items.
                    foreach( var g in EngineMap.AllTypesAttributesCache.Values.SelectMany( attr => attr.GetAllCustomAttributes<ICodeGenerator>() ) )
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
                }
                if( errorSummary != null )
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
                return true;
            }
            catch( Exception ex )
            {
                monitor.Error( $"While generating final source code.", ex );
                return false;
            }
        }

        /// <summary>
        /// Finalizes the source code generation and compilation.
        /// Sources (if <see cref="ICodeGenerationContext.SaveSource"/> is true) will be generated
        /// in the <paramref name="finalFilePath"/> folder.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="finalFilePath">The final generated assembly full path.</param>
        /// <param name="codeGenContext">The code generation context.</param>
        /// <param name="secondPass">
        /// The list of second passes actions to apply on the <see cref="ICodeGenerationContext"/> before
        /// generating the source file and compiling them (<see cref="ICodeGenerationContext.CompileOption"/>).
        /// </param>
        /// <param name="availableStObjMap">
        /// Predicate that states whether a signature can be bound to an already available StObjMap.
        /// When true is returned, the process stops as early as possible and the available map should be used.
        /// </param>
        /// <returns>A Code generation result.</returns>
        public CodeGenerateResult GenerateSourceCodeSecondPass(
            IActivityMonitor monitor,
            string finalFilePath,
            ICodeGenerationContext codeGenContext,
            List<MultiPassCodeGeneration> secondPass,
            Func<SHA1Value,bool> availableStObjMap )
        {
            if( EngineMap == null ) throw new InvalidOperationException( nameof( HasFatalError ) );
            if( codeGenContext.Assembly != _tempAssembly ) throw new ArgumentException( "CodeGenerationContext mismatch.", nameof( codeGenContext ) );
            List<string> generatedFileNames = new List<string>();
            try
            {
                IReadOnlyList<ActivityMonitorSimpleCollector.Entry>? errorSummary = null;
                using( monitor.OpenInfo( $"Generating source code (second pass) for: {codeGenContext.CurrentRun.Names}." ) )
                using( monitor.CollectEntries( entries => errorSummary = entries ) )
                {
                    MultiPassCodeGeneration.RunSecondPass( monitor, codeGenContext, secondPass );
                }
                if( errorSummary != null )
                {
                    using( monitor.OpenFatal( $"{errorSummary.Count} error(s). Summary:" ) )
                    {
                        foreach( var e in errorSummary )
                        {
                            monitor.Trace( $"{e.MaskedLevel} - {e.Text}" );
                        }
                    }
                    return new CodeGenerateResult( false, generatedFileNames );
                }
                // Code generation itself succeeds.
                if( !codeGenContext.SaveSource && codeGenContext.CompileOption == CompileOption.None )
                {
                    monitor.Info( "Configured GenerateSourceFile is false and CompileOption is None: nothing more to do." );
                    return new CodeGenerateResult( true, generatedFileNames );
                }
                SHA1Value signature = SHA1Value.ZeroSHA1;

                // Trick to avoid allocating the big string code more than once: the hash is first computed on the source
                // without the signature header, a part is injected at the top of the file (before anything else) and
                // the final big string is built only once.
                var ws = codeGenContext.Assembly.Code;
                if( signature.IsZero || signature == SHA1Value.EmptySHA1 )
                {
                    using( var s = new SHA1Stream() )
                    using( var w = new StreamWriter( s ) )
                    {
                        ws.WriteGlobalSource( w );
                        w.Flush();
                        signature = s.GetFinalResult();
                    }
                    monitor.Info( $"Computed file signature: {signature}." );
                }
                else
                {
                    monitor.Info( $"Using provided file signature: {signature}." );
                }

                if( availableStObjMap( signature ) )
                {
                    monitor.Info( "An existing StObjMap with the signature exists: skipping the generation." );
                    return new CodeGenerateResult( true, generatedFileNames, signature );
                }

                // Injects the SHA1 signature at the top.
                ws.Global.BeforeNamespace.CreatePart( top: true ).Append( @"[assembly: CK.StObj.Signature( " ).AppendSourceString( signature.ToString() ).Append( " )]" );

                // The source code is available.
                string code = ws.GetGlobalSource();

                // If asked to do so, we always save it, even if parsing or compilation fails. 
                if( codeGenContext.SaveSource )
                {
                    var sourceFile = finalFilePath + ".cs";
                    File.WriteAllText( sourceFile, code );
                    generatedFileNames.Add( Path.GetFileName( sourceFile ) );
                    monitor.Info( $"Saved source file: {sourceFile}." );
                }
                if( codeGenContext.CompileOption == CompileOption.None )
                {
                    monitor.Info( "Configured CompileOption is None: nothing more to do." );
                    return new CodeGenerateResult( true, generatedFileNames, signature );
                }

                using( monitor.OpenInfo( codeGenContext.CompileOption == CompileOption.Compile
                                            ? "Compiling source code (using C# v8.0 language version)."
                                            : "Only parsing source code, using C# v8.0 language version (skipping compilation)." ) )
                {
                    var result = CodeGenerator.Generate( code,
                                                         codeGenContext.CompileOption == CompileOption.Parse ? null : finalFilePath,
                                                         ws.AssemblyReferences,
                                                         new CSharpParseOptions( LanguageVersion.CSharp8 ) );

                    if( result.Success && codeGenContext.CompileOption == CompileOption.Compile )
                    {
                        generatedFileNames.Add( Path.GetFileName( finalFilePath ) );
                    }
                    result.LogResult( monitor );
                    if( !result.Success )
                    {
                        monitor.Debug( code );
                        monitor.CloseGroup( "Failed" );
                    }
                    return new CodeGenerateResult( result.Success, generatedFileNames, signature );
                }
                
            }
            catch( Exception ex )
            {
                monitor.Error( $"While generating final assembly '{finalFilePath}' from source code.", ex );
                return new CodeGenerateResult( false, generatedFileNames );
            }
        }

        const string _sourceGStObj = @"
class GStObj : IStObj
{
    public GStObj( Type t, IStObj g, IStObjMap m, int idx )
    {
        ClassType = t;
        Generalization = g;
        StObjMap = m;
        IndexOrdered = idx;
    }

    public Type ClassType { get; }

    public IStObj Generalization { get; }

    public IStObjMap StObjMap { get; }

    public IStObj Specialization { get; internal set; }

    public IStObjFinalImplementation FinalImplementation { get; internal set; }

    public int IndexOrdered { get; }

    internal StObjMapping AsMapping => new StObjMapping( this, FinalImplementation );
}";
        const string _sourceFinalGStObj = @"
class GFinalStObj : GStObj, IStObjFinalImplementation
{
    public GFinalStObj( object impl, Type actualType, IReadOnlyCollection<Type> mult, IReadOnlyCollection<Type> uniq, Type t, IStObj g, IStObjMap m, int idx )
            : base( t, g, m, idx )
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

            ns.Append( _sourceGStObj ).NewLine();
            ns.Append( _sourceFinalGStObj ).NewLine();

            var rootType = ns.CreateType( "sealed class " + StObjContextRoot.RootContextTypeName + " : IStObjMap, IStObjObjectMap, IStObjServiceMap" )
                                .Append( "readonly GStObj[] _stObjs;" ).NewLine()
                                .Append( "readonly GFinalStObj[] _finalStObjs;" ).NewLine()
                                .Append( "readonly Dictionary<Type,GFinalStObj> _map;" ).NewLine();

            var rootCtor = rootType.CreateFunction( $"public {StObjContextRoot.RootContextTypeName}( IActivityMonitor monitor )" );

            rootCtor.Append( $"_stObjs = new GStObj[{orderedStObjs.Count}];" ).NewLine()
                    .Append( $"_finalStObjs = new GFinalStObj[{CKTypeResult.RealObjects.EngineMap.FinalImplementations.Count}];" ).NewLine();
            int iStObj = 0;
            int iImplStObj = 0;
            foreach( var m in orderedStObjs )
            {
                Debug.Assert( (m.Specialization != null) == (m != m.FinalImplementation) );
                string generalization = m.Generalization == null ? "null" : $"_stObjs[{m.Generalization.IndexOrdered}]";
                rootCtor.Append( $"_stObjs[{iStObj++}] = " );
                if( m.Specialization == null )
                {
                    rootCtor.Append( "_finalStObjs[" ).Append( iImplStObj++ ).Append( "] = new GFinalStObj( new " )
                            .AppendCSharpName( m.FinalImplementation.FinalType ).Append("(), " )
                            .AppendTypeOf( m.FinalImplementation.FinalType ).Append( ", " ).NewLine()
                            .AppendArray( m.FinalImplementation.MultipleMappings ).Append( ", " ).NewLine()
                            .AppendArray( m.FinalImplementation.UniqueMappings ).Append( ", " ).NewLine();
                }
                else rootCtor.Append( "new GStObj(" );

                rootCtor.AppendTypeOf( m.ClassType ).Append( ", " )
                        .Append( generalization )
                        .Append( ", this, " ).Append( m.IndexOrdered ).Append( " );" ).NewLine();

            }

            rootCtor.Append( $"_map = new Dictionary<Type,GFinalStObj>();" ).NewLine();
            var allMappings = CKTypeResult.RealObjects.EngineMap.RawMappings;
            // We skip highest implementation Type mappings (ie. RealObjectInterfaceKey keys) since 
            // there is no ToHead mapping (to root generalization) on final (runtime) IStObjMap.
            foreach( var e in allMappings.Where( e => e.Key is Type ) )
            {
                rootCtor.Append( $"_map.Add( " ).AppendTypeOf( (Type)e.Key )
                        .Append( ", (GFinalStObj)_stObjs[" ).Append( e.Value.IndexOrdered ).Append( "] );" ).NewLine();
            }
            if( orderedStObjs.Count > 0 )
            {
                rootCtor.Append( $"int iStObj = {orderedStObjs.Count};" ).NewLine()
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
            var propertyCache = new Dictionary<ValueTuple<Type, string>, string>();
            foreach( MutableItem m in orderedStObjs )
            {
                if( m.PreConstructProperties != null )
                {
                    foreach( var setter in m.PreConstructProperties )
                    {
                        Debug.Assert( setter.Property.DeclaringType != null );
                        Type decl = setter.Property.DeclaringType;
                        string? varName;
                        var key = ValueTuple.Create( decl, setter.Property.Name );
                        if(!propertyCache.TryGetValue( key, out varName ))
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
                               .Append( m.IndexOrdered).Append( "].FinalImplementation.Implementation," );
                        GenerateValue( rootCtor, setter.Value );
                        rootCtor.Append( ");" ).NewLine();
                    }
                }
                if( m.ConstructParametersAbove != null )
                {
                    foreach( var mp in m.ConstructParametersAbove )
                    {
                        Debug.Assert( mp.Item2.Count > 0 );
                        rootCtor.AppendTypeOf( mp.Item1.DeclaringType! );
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
            foreach( MutableItem m in orderedStObjs )
            {
                foreach( MethodInfo init in m.RealObjectType.AllStObjInitialize )
                {
                    if( init == m.RealObjectType.StObjInitialize ) rootCtor.Append( $"_stObjs[{m.IndexOrdered}].ClassType" );
                    else rootCtor.AppendTypeOf( init.DeclaringType! );

                    rootCtor.Append( ".GetMethod(")
                            .AppendSourceString( StObjContextRoot.InitializeMethodName )
                            .Append( ", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly )" )
                            .NewLine();
                    rootCtor.Append( $".Invoke( _stObjs[{m.IndexOrdered}].FinalImplementation.Implementation, new object[]{{ monitor, this }} );" )
                            .NewLine();
                }
            }

            rootType.Append( "" ).NewLine()
                    .Append( @"
            public IStObjObjectMap StObjs => this;

            IReadOnlyList<string> IStObjMap.Names => CK.StObj.SignatureAttribute.V.Names;
            SHA1Value IStObjMap.GeneratedSignature => CK.StObj.SignatureAttribute.V.Signature;

            IStObj IStObjObjectMap.ToLeaf( Type t ) => GToLeaf( t );
            object IStObjObjectMap.Obtain( Type t ) => _map.TryGetValue( t, out var s ) ? s.Implementation : null;

            IEnumerable<IStObjFinalImplementation> IStObjObjectMap.FinalImplementations => _finalStObjs;

            IEnumerable<StObjMapping> IStObjObjectMap.StObjs => _stObjs.Select( s => s.AsMapping );

            GFinalStObj GToLeaf( Type t ) => _map.TryGetValue( t, out var s ) ? s : null;
            " );

            var serviceGen = new ServiceSupportCodeGenerator( rootType, rootCtor );
            serviceGen.CreateServiceSupportCode( EngineMap.Services );
            serviceGen.CreateConfigureServiceMethod( orderedStObjs );

            GenerateVFeatures( monitor, rootType, rootCtor, EngineMap.Features );
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

        void GenerateVFeatures( IActivityMonitor monitor, ITypeScope rootType, IFunctionScope rootCtor, IReadOnlyCollection<VFeature> features )
        {
            monitor.Info( $"Generating VFeatures: {features.Select( f => f.ToString()).Concatenate()}." );

            rootType.Append( "readonly IReadOnlyCollection<VFeature> _vFeatures;" ).NewLine();

            rootCtor.Append( "_vFeatures = new VFeature[]{ " );
            bool atleastOne = false;
            foreach( var f in features )
            {
                if( atleastOne ) rootCtor.Append( ", " );
                atleastOne = true;
                rootCtor.Append( "new VFeature( " )
                        .AppendSourceString( f.Name )
                        .Append(',')
                        .Append( "CSemVer.SVersion.Parse( " )
                        .AppendSourceString( f.Version.ToNormalizedString() )
                        .Append( " ) )" );
            }
            rootCtor.Append( "};" );
            rootType.Append( "public IReadOnlyCollection<VFeature> Features => _vFeatures;" ).NewLine();
        }

        static void GenerateValue( ICodeWriter b, object? o )
        {
            if( o is IActivityMonitor )
            {
                b.Append( "monitor" );
            }
            else if( o is MutableItem )
            {
                b.Append( $"_stObjs[{((MutableItem)o).IndexOrdered}].FinalImplementation.Implementation" );
            }
            else
            {
                b.Append( o );
            }
        }
    }
}




