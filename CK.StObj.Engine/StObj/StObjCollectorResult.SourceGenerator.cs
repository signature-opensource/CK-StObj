using System;
using System.Collections.Generic;
using System.Linq;
using CK.Core;
using System.Reflection;
using System.IO;
using CK.CodeGen;
using CK.Text;
using CK.CodeGen.Abstractions;
using Microsoft.CodeAnalysis.CSharp;
using System.Diagnostics;

namespace CK.Setup
{
    public partial class StObjCollectorResult
    {
        /// <summary>
        /// Captures code generation result.
        /// </summary>
        public readonly struct CodeGenerateResult
        {
            /// <summary>
            /// Gets whether the generation succeeded.
            /// </summary>
            public readonly bool Success;

            /// <summary>
            /// Gets the list of files that have been generated: the assembly itself and
            /// any source code or other files.
            /// </summary>
            public readonly IReadOnlyList<string> GeneratedFileNames;

            internal CodeGenerateResult( bool success, IReadOnlyList<string> fileNames )
            {
                Success = success;
                GeneratedFileNames = fileNames;
            }
        }

        CodeGenerateResult GenerateSourceCode( IActivityMonitor monitor, string finalFilePath, bool saveSource, bool skipCompilation )
        {
            List<string> generatedFileNames = new List<string>();
            try
            {
                // Retrieves CK._g workspace.
                var ws = _tempAssembly.DefaultGenerationNamespace.Workspace;

                IReadOnlyList<ActivityMonitorSimpleCollector.Entry> errorSummary = null;
                using( monitor.OpenInfo( "Generating source code." ) )
                using( monitor.CollectEntries( entries => errorSummary = entries ) )
                {
                    // Injects System.Reflection and setup assemblies into the
                    // workspace that will be used to generate source code.
                    ws.EnsureAssemblyReference( typeof( BindingFlags ) );
                    if( CKTypeResult.Assemblies.Count > 0 )
                    {
                        ws.EnsureAssemblyReference( CKTypeResult.Assemblies );
                    }
                    else
                    {
                        ws.EnsureAssemblyReference( typeof(StObjContextRoot).Assembly );
                    }
                    // Gets the global name space and injects, once for all, basic namespaces that we
                    // always want available.
                    var global = ws.Global.EnsureUsing( "CK.Core" )
                                          .EnsureUsing( "System" )
                                          .EnsureUsing( "System.Collections.Generic" )
                                          .EnsureUsing( "System.Linq" )
                                          .EnsureUsing( "System.Text" )
                                          .EnsureUsing( "System.Reflection" );

                    // Asks every ImplementableTypeInfo to generate their code. 
                    // This step MUST always be done, even if SkipCompilation is true and GenerateSourceFiles is false
                    // since during this step, side effects MAY occur (this is typically the case of the first run where
                    // the "reality cache" is created).
                    foreach( var t in CKTypeResult.TypesToImplement )
                    {
                        t.GenerateType( monitor, _tempAssembly );
                    }

                    // Generates the StObjContextRoot implementation.
                    GenerateStObjContextRootSource( monitor, global.FindOrCreateNamespace( "CK.StObj" ) );
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
                using( monitor.OpenInfo( skipCompilation
                                            ? "Generating source code, parsing using C# v7.3 language version, skipping compilation."
                                            : "Compiling source code (using C# v7.3 language version)." ) )
                {
                    var g = new CodeGenerator( CodeWorkspace.Factory );
                    g.ParseOptions = new CSharpParseOptions( LanguageVersion.CSharp7_3 );
                    g.Modules.AddRange( _tempAssembly.SourceModules );
                    var result = g.Generate( ws, finalFilePath, skipCompilation );
                    if( saveSource && result.Sources != null )
                    {
                        for( int i = 0; i < result.Sources.Count; ++i )
                        {
                            string sourceFile = $"{finalFilePath}.{i}.cs";
                            monitor.Info( $"Saved source file: {sourceFile}" );
                            File.WriteAllText( sourceFile, result.Sources[i].ToString() );
                            generatedFileNames.Add( Path.GetFileName( sourceFile ) );
                        }
                    }
                    if( result.Success ) generatedFileNames.Add( Path.GetFileName( finalFilePath ) );
                    result.LogResult( monitor );
                    return new CodeGenerateResult( result.Success, generatedFileNames );
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
    public GStObj( IStObjRuntimeBuilder rb, Type t, IStObj g, Type actualType, IStObjMap m )
    {
        StObjMap = m;
        ObjectType = t;
        Generalization = g;
        if( actualType != null ) 
        {
            Instance = rb.CreateInstance( actualType );
            Leaf = this;
        }
    }

    public IStObjMap StObjMap { get; }

    public Type ObjectType { get; }

    public IStObj Generalization { get; }

    public IStObj Specialization { get; internal set; }

    internal object Instance;
    
    internal GStObj Leaf;

    internal StObjImplementation AsStObjImplementation => new StObjImplementation( this, Instance );
}";
        void GenerateStObjContextRootSource( IActivityMonitor monitor, INamespaceScope ns )
        {
            ns.Append( _sourceGStObj ).NewLine();

            var rootType = ns.CreateType( "public class " + StObjContextRoot.RootContextTypeName + " : IStObjMap, IStObjObjectMap, IStObjServiceMap" )
                                .Append( "readonly GStObj[] _stObjs;" ).NewLine()
                                .Append( "readonly GStObj[] _implStObjs;" ).NewLine()
                                .Append( "readonly Dictionary<Type,GStObj> _map;" ).NewLine();

            var rootCtor = rootType.CreateFunction( $"public {StObjContextRoot.RootContextTypeName}(IActivityMonitor monitor, IStObjRuntimeBuilder rb)" );

            rootCtor.Append( $"_stObjs = new GStObj[{OrderedStObjs.Count}];" ).NewLine()
                    .Append( $"_implStObjs = new GStObj[{CKTypeResult.RealObjects.EngineMap.AllSpecializations.Count}];" ).NewLine();
            int iStObj = 0;
            int iImplStObj = 0;
            foreach( MutableItem m in OrderedStObjs )
            {
                string generalization = m.Generalization == null ? "null" : $"_stObjs[{m.Generalization.IndexOrdered}]";
                rootCtor.Append( $"_stObjs[{iStObj++}] = " );
                if( m.Specialization == null )
                {
                    rootCtor.Append( $"_implStObjs[{iImplStObj++}] = " );
                }
                rootCtor.Append( "new GStObj(" )
                       .Append( "rb, " )
                       .AppendTypeOf( m.ObjectType ).Append(", ")
                       .Append( generalization ).Append( ", ")
                       .AppendTypeOf( m.Specialization == null ? m.ImplementableTypeInfo?.StubType ?? m.Type.Type : null )
                       .Append( ", this );" )
                       .NewLine();
            }

            rootCtor.Append( $"_map = new Dictionary<Type,GStObj>();" ).NewLine();
            var allMappings = CKTypeResult.RealObjects.EngineMap.RawMappings;
            // We skip highest implementation Type mappings (ie. RealObjectInterfaceKey keys) since 
            // there is no ToStObj mapping (to root generalization) on final (runtime) IStObjMap.
            foreach( var e in allMappings.Where( e => e.Key is Type ) )
            {
                rootCtor.Append( $"_map.Add( " ).AppendTypeOf( (Type)e.Key )
                       .Append( ", _stObjs[").Append( e.Value.IndexOrdered ).Append( "] );" ).NewLine();
            }
            if( OrderedStObjs.Count > 0 )
            {
                rootCtor.Append( $"int iStObj = {OrderedStObjs.Count};" ).NewLine()
                       .Append( "while( --iStObj >= 0 ) {" ).NewLine()
                       .Append( " var o = _stObjs[iStObj];" ).NewLine()
                       .Append( " if( o.Specialization == null ) {" ).NewLine()
                       .Append( "  GStObj g = (GStObj)o.Generalization;" ).NewLine()
                       .Append( "  while( g != null ) {" ).NewLine()
                       .Append( "   g.Specialization = o;" ).NewLine()
                       .Append( "   g.Instance = o.Instance;" ).NewLine()
                       .Append( "   g.Leaf = o.Leaf;" ).NewLine()
                       .Append( "   o = g;" ).NewLine()
                       .Append( "   g = (GStObj)o.Generalization;" ).NewLine()
                       .Append( "  }" ).NewLine()
                       .Append( " }" ).NewLine()
                       .Append( "}" ).NewLine();
            }
            var propertyCache = new Dictionary<ValueTuple<Type, string>, string>();
            foreach( MutableItem m in OrderedStObjs )
            {
                if( m.PreConstructProperties != null )
                {
                    foreach( var setter in m.PreConstructProperties )
                    {
                        Type decl = setter.Property.DeclaringType;
                        string varName;
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
                               .Append( m.IndexOrdered).Append( "].Instance," );
                        GenerateValue( rootCtor, setter.Value );
                        rootCtor.Append( ");" ).NewLine();
                    }
                }
                if( m.ConstructParametersAbove != null )
                {
                    foreach( var mp in m.ConstructParametersAbove )
                    {
                        Debug.Assert( mp.Item2.Count > 0 );
                        rootCtor.AppendTypeOf( mp.Item1.DeclaringType );
                        CallConstructMethod( rootCtor, m, mp.Item2 );
                    }
                }
                if( m.Type.StObjConstruct != null )
                {
                    Debug.Assert( m.ConstructParameters.Count > 0 );
                    rootCtor.Append( $"_stObjs[{m.IndexOrdered}].ObjectType" );
                    CallConstructMethod( rootCtor, m, m.ConstructParameters );
                }
            }
            foreach( MutableItem m in OrderedStObjs )
            {
                if( m.PostBuildProperties != null )
                {
                    foreach( var p in m.PostBuildProperties )
                    {
                        Type decl = p.Property.DeclaringType;
                        rootCtor.AppendTypeOf( decl )
                               .Append( $".GetProperty( \"{p.Property.Name}\", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic )" )
                               .Append( $".SetValue(_stObjs[{m.IndexOrdered}].Instance, " );
                        GenerateValue( rootCtor, p.Value );
                        rootCtor.Append( ");" ).NewLine();
                    }
                }
            }
            foreach( MutableItem m in OrderedStObjs )
            {
                foreach( MethodInfo init in m.Type.AllStObjInitialize )
                {
                    if( init == m.Type.StObjInitialize ) rootCtor.Append( $"_stObjs[{m.IndexOrdered}].ObjectType" );
                    else rootCtor.AppendTypeOf( init.DeclaringType );

                    rootCtor.Append( ".GetMethod(")
                            .AppendSourceString( StObjContextRoot.InitializeMethodName )
                            .Append( ", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly )" )
                            .NewLine();
                    rootCtor.Append( $".Invoke( _stObjs[{m.IndexOrdered}].Instance, new object[]{{ monitor, this }} );" )
                            .NewLine();
                }
            }

            rootType.Append( "public string MapName => " ).AppendSourceString( MapName ).Append( ";" ).NewLine()
                    .Append( @"
            public IStObjObjectMap StObjs => this;

            Type IStObjTypeMap.ToLeafType( Type t ) => GToLeaf( t )?.ObjectType;
            bool IStObjTypeMap.IsMapped( Type t ) => _map.ContainsKey( t );
            IEnumerable<Type> IStObjTypeMap.Types => _map.Keys;

            IStObj IStObjObjectMap.ToLeaf( Type t ) => GToLeaf( t );
            object IStObjObjectMap.Obtain( Type t ) => _map.TryGetValue( t, out var s ) ? s.Instance : null;
            IEnumerable<object> IStObjObjectMap.Implementations => _implStObjs.Select( s => s.Instance );
            IEnumerable<StObjImplementation> IStObjObjectMap.StObjs => _implStObjs.Select( s => s.AsStObjImplementation );
            IEnumerable<KeyValuePair<Type, object>> IStObjObjectMap.Mappings => _map.Select( v => new KeyValuePair<Type, object>( v.Key, v.Value.Instance ) );
            GStObj GToLeaf( Type t ) => _map.TryGetValue( t, out var s ) ? s.Leaf : null;
            " );

            var serviceGen = new ServiceSupportCodeGenerator( rootType, rootCtor );
            serviceGen.CreateServiceSupportCode( _liftedMap );
            serviceGen.CreateConfigureServiceMethod( OrderedStObjs );

            GenerateVFeatures( monitor, rootType, rootCtor, _liftedMap.Features );
        }

        static void CallConstructMethod( IFunctionScope rootCtor, MutableItem m, IEnumerable<IStObjMutableParameter> parameters )
        {
            rootCtor.Append( ".GetMethod(" )
                    .AppendSourceString( StObjContextRoot.ConstructMethodName )
                    .Append( ", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly )" )
                    .Append( $".Invoke( _stObjs[{m.IndexOrdered}].Instance, new object[] {{" );
            // Missing Value {get;} on IStObjMutableParameter and we need the BuilderValueIndex...
            // Hideous downcast for the moment.
            foreach( var p in parameters.Cast<MutableParameter>() )
            {
                if( p.BuilderValueIndex < 0 )
                {
                    rootCtor.Append( $"_stObjs[{-(p.BuilderValueIndex + 1)}].Instance" );
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

        static void GenerateValue( ICodeWriter b, object o )
        {
            if( o is IActivityMonitor )
            {
                b.Append( "monitor" );
            }
            else if( o is MutableItem )
            {
                b.Append( $"_stObjs[{((MutableItem)o).IndexOrdered}].Instance" );
            }
            else
            {
                b.Append( o );
            }
        }
    }
}




