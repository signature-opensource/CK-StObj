using CK.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Describes a folder to process and for which a <see cref="IStObjMap"/> should be generated,
    /// either in source code or as compiled Dynamic Linked Library.
    /// <para>
    /// These configuration objects are shared with CKSetup configuration: CKSetup handles only the &lt;BinPath Path="..." /&gt;
    /// element on input (and adds a BinPath="..." attribute that contains the actual bin path folder - typically ending with /publish
    /// after its work and before calling the StObj engine.
    /// </para>
    /// </summary>
    public sealed class BinPathConfiguration
    {
        readonly Dictionary<string,BinPathAspectConfiguration> _aspects;

        /// <summary>
        /// Models the &lt;Type&gt; elements that are children of &lt;Types&gt;.
        /// </summary>
        public sealed class TypeConfiguration
        {
            /// <summary>
            /// Initializes a new <see cref="TypeConfiguration"/> from a Xml element.
            /// </summary>
            /// <param name="e">The Xml element.</param>
            public TypeConfiguration( XElement e )
            {
                Name = (string?)e.Attribute( EngineConfiguration.xName ) ?? e.Value;
                var k = (string?)e.Attribute( EngineConfiguration.xKind );
                if( k != null ) Kind = (AutoServiceKind)Enum.Parse( typeof( AutoServiceKind ), k.Replace( '|', ',' ) );
                Optional = (bool?)e.Attribute( EngineConfiguration.xOptional ) ?? false;
            }

            /// <summary>
            /// Initializes a new <see cref="TypeConfiguration"/>.
            /// </summary>
            /// <param name="name">Assembly qualified name of the type.</param>
            /// <param name="kind">The service kind.</param>
            /// <param name="optional">Whether the type may not exist.</param>
            public TypeConfiguration( string name, AutoServiceKind kind, bool optional )
            {
                Name = name;
                Kind = kind;
                Optional = optional;
            }

            /// <summary>
            /// Gets or sets the assembly qualified name of the type.
            /// This should not be null or whitespace, nor appear more than once in the <see cref="Types"/> collection otherwise
            /// this configuration is considered invalid.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Gets or sets the service kind. Defaults to <see cref="AutoServiceKind.None"/>.
            /// Note that this None value may be used along with a false <see cref="Optional"/> to check the existence
            /// of a type.
            /// </summary>
            public AutoServiceKind Kind { get; set; }

            /// <summary>
            /// Gets or sets whether this type is optional: if the <see cref="Name"/> cannot be resolved
            /// a warning is emitted.
            /// Defaults to false: by default, if the type is not found at runtime, an error is raised.
            /// </summary>
            public bool Optional { get; set; }

            /// <summary>
            /// Overridden to return the Name - Kind and Optional value.
            /// This is used as the equality key when configurations are grouped into similar bin paths.
            /// </summary>
            /// <returns>A readable string.</returns>
            public override string ToString() => $"{Name} - {Kind} - {Optional}";
        }

        /// <summary>
        /// Initializes a new empty <see cref="BinPathConfiguration"/>.
        /// At least, the <see cref="Path"/> should be set for this BinPathConfiguration to be valid.
        /// </summary>
        public BinPathConfiguration()
        {
            GenerateSourceFiles = true;
            Assemblies = new HashSet<string>();
            ExcludedTypes = new HashSet<string>();
            Types = new List<TypeConfiguration>();
            _aspects = new Dictionary<string, BinPathAspectConfiguration>();
        }

        internal BinPathConfiguration( EngineConfiguration configuration, XElement e, Dictionary<string, EngineAspectConfiguration> namedAspects )
        {
            Owner = configuration;
            Name = (string?)e.Attribute( EngineConfiguration.xName );
            Path = (string?)e.Attribute( EngineConfiguration.xPath );
            OutputPath = (string?)e.Element( EngineConfiguration.xOutputPath );
            ProjectPath = (string?)e.Element( EngineConfiguration.xProjectPath );

            if( e.Element( "SkipCompilation" ) != null )
            {
                throw new XmlException( @"Element SkipCompilation must be replaced with CompileOption that can be be ""None"", ""Parse"" or ""Compile"". It defaults to ""None""." );
            }
            CompileOption = e.Element( EngineConfiguration.xCompileOption )?.Value.ToUpperInvariant() switch
            {
                null => CompileOption.None,
                "NONE" => CompileOption.None,
                "PARSE" => CompileOption.Parse,
                "COMPILE" => CompileOption.Compile,
                _ => throw new XmlException( @"Expected CompileOption to be ""None"", ""Parse"" or ""Compile""." )
            };

            GenerateSourceFiles = (bool?)e.Element( EngineConfiguration.xGenerateSourceFiles ) ?? true;

            Assemblies = new HashSet<string>( EngineConfiguration.FromXml( e, EngineConfiguration.xAssemblies, EngineConfiguration.xAssembly ) );
            ExcludedTypes = new HashSet<string>( EngineConfiguration.FromXml( e, EngineConfiguration.xExcludedTypes, EngineConfiguration.xType ) );

            Types = e.Elements( EngineConfiguration.xTypes ).Elements( EngineConfiguration.xType ).Select( c => new TypeConfiguration( c ) ).ToList();

            var allowedNames = new List<string>()
            {
                EngineConfiguration.xTypes.ToString(),
                EngineConfiguration.xExcludedTypes.ToString(),
                EngineConfiguration.xAssemblies.ToString(),
                EngineConfiguration.xGenerateSourceFiles.ToString(),
                EngineConfiguration.xCompileOption.ToString(),
                EngineConfiguration.xProjectPath.ToString(),
                EngineConfiguration.xOutputPath.ToString(),
                EngineConfiguration.xPath.ToString(),
                EngineConfiguration.xName.ToString()
            };
            _aspects = new Dictionary<string, BinPathAspectConfiguration>();
            var aspectConfigurations = e.Elements().Where( e => !allowedNames.Contains( e.Name.LocalName ) ).ToList();
            foreach( var c in aspectConfigurations )
            {
                if( !namedAspects.TryGetValue( c.Name.LocalName, out var aspectType ) )
                {
                    var expectedAspects = "";
                    if( namedAspects.Count > 0 )
                    {
                        expectedAspects = $" or aspect's BinPath configurations '{namedAspects.Keys.Concatenate( "', '" )}'";
                    }
                    Throw.InvalidDataException( $"Unexpected element name '{c.Name.LocalName}'. Expected: '{allowedNames.Concatenate( "', '" )}'{expectedAspects}." );
                }
                if( _aspects.ContainsKey( aspectType.AspectName ) )
                {
                    Throw.InvalidDataException( $"Duplicated element name '{c.Name.LocalName}'. At most one BinPath aspect configuration of a given type can exist." );
                }
                var a = aspectType.CreateBinPathConfiguration();
                if( a.AspectName != aspectType.AspectName )
                {
                    Throw.CKException( $"Aspect '{aspectType.AspectName}' created an aspect of type '{a.AspectName}BinPathAspectConfiguration'. The type name should be ''{aspectType.AspectName}BinPathAspectConfiguration''." );
                }
                a.InitializeFrom( c );
                _aspects.Add( a.AspectName, a );
            }
        }

        /// <summary>
        /// Creates a xml element from this <see cref="BinPathConfiguration"/>.
        /// </summary>
        /// <returns>A new element.</returns>
        public XElement ToXml()
        {
            return new XElement( EngineConfiguration.xBinPath,
                                    String.IsNullOrWhiteSpace( Name ) ? null : new XAttribute( EngineConfiguration.xName, Name ),
                                    new XAttribute( EngineConfiguration.xPath, Path ),
                                    !OutputPath.IsEmptyPath ? new XElement( EngineConfiguration.xOutputPath, OutputPath ) : null,
                                    !ProjectPath.IsEmptyPath ? new XElement( EngineConfiguration.xProjectPath, ProjectPath ) : null,
                                    new XElement( EngineConfiguration.xCompileOption, CompileOption.ToString() ),
                                    GenerateSourceFiles ? null : new XElement( EngineConfiguration.xGenerateSourceFiles, false ),
                                    EngineConfiguration.ToXml( EngineConfiguration.xAssemblies, EngineConfiguration.xAssembly, Assemblies ),
                                    EngineConfiguration.ToXml( EngineConfiguration.xExcludedTypes, EngineConfiguration.xType, ExcludedTypes ),
                                    new XElement( EngineConfiguration.xTypes,
                                                    Types.Select( t => new XElement( EngineConfiguration.xType,
                                                                            new XAttribute( EngineConfiguration.xName, t.Name ),
                                                                            t.Kind != AutoServiceKind.None ? new XAttribute( EngineConfiguration.xKind, t.Kind ) : null,
                                                                            t.Optional ? new XAttribute( EngineConfiguration.xOptional, true ) : null ) ) ),
                                    _aspects.Values.Select( a => a.ToXml() ) );
        }

        /// <summary>
        /// Gets the configuration that contains this BinPath in its <see cref="EngineConfiguration.BinPaths"/>.
        /// </summary>
        public EngineConfiguration? Owner { get; internal set; }

        /// <summary>
        /// Gets or sets the name that uniquely identifies this configuration among the others.
        /// When null, an automatically numbered name is generated.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the path of the directory to setup (this property is shared with CKSetup configuration).
        /// It can be relative: it will be combined to the <see cref="EngineConfiguration.BasePath"/>.
        /// <para>
        /// Nothing prevents multiple <see cref="BinPathConfiguration"/> to have the same Path. In such case, <see cref="OutputPath"/>
        /// and/or <see cref="ProjectPath"/> should be set to different directories (otherwise file generation will be in trouble).
        /// </para>
        /// </summary>
        public NormalizedPath Path { get; set; }

        /// <summary>
        /// Gets or sets an optional target (output) directory where generated files (assembly and/or sources)
        /// must be copied. When <see cref="NormalizedPath.IsEmptyPath"/>, this <see cref="Path"/> is used.
        /// </summary>
        public NormalizedPath OutputPath { get; set; }

        /// <summary>
        /// Gets or sets an optional target (output) directory for source files.
        /// When not <see cref="NormalizedPath.IsEmptyPath"/>, "$StObjGen/" folder is appended and
        /// the source files are generated into this folder instead of <see cref="OutputPath"/>.
        /// </summary>
        public NormalizedPath ProjectPath { get; set; }

        /// <summary>
        /// Gets or sets the Roslyn compilation behavior.
        /// Defaults to <see cref="CompileOption.None"/>.
        /// </summary>
        public CompileOption CompileOption { get; set; }

        /// <summary>
        /// Gets whether generated source files should be generated and copied to <see cref="OutputPath"/>.
        /// Defaults to true.
        /// </summary>
        public bool GenerateSourceFiles { get; set; }

        /// <summary>
        /// Gets a set of assembly names that must be processed for setup (only assemblies that appear in this list will be considered).
        /// Note that when using CKSetup, this list can be left empty: it is automatically filled with the "model" and "model dependent"
        /// assemblies.
        /// </summary>
        public HashSet<string> Assemblies { get; }

        /// <summary>
        /// Gets a set of <see cref="TypeConfiguration"/> that must be configured explicitly.
        /// Type names that appear here with <see cref="TypeConfiguration.Optional"/> set to false are registered
        /// regardless of the <see cref="Assemblies"/>.
        /// </summary>
        public List<TypeConfiguration> Types { get; }

        /// <summary>
        /// Adds a <see cref="TypeConfiguration"/> to <see cref="Types"/>.
        /// </summary>
        /// <param name="type">The type to configure.</param>
        /// <param name="kind">The kind to set.</param>
        /// <param name="isOptional">Whether the type may not exist.</param>
        /// <returns></returns>
        public BinPathConfiguration AddType( Type type, AutoServiceKind kind, bool isOptional )
        {
            Throw.CheckNotNullArgument( type );
            Throw.CheckArgument( type.AssemblyQualifiedName != null );
            Types.Add( new TypeConfiguration( type.AssemblyQualifiedName, kind, isOptional ) );
            return this;
        }

        /// <summary>
        /// Adds a <see cref="TypeConfiguration"/> to <see cref="Types"/>.
        /// </summary>
        /// <param name="name">The assembly qualified name of the type.</param>
        /// <param name="kind">The kind to set.</param>
        /// <param name="isOptional">Whether the type may not exist.</param>
        /// <returns>This BinPathConfiguration (fluent syntax).</returns>
        public BinPathConfiguration AddType( string name, AutoServiceKind kind, bool isOptional )
        {
            Throw.CheckNotNullArgument( name );
            Types.Add( new TypeConfiguration( name, kind, isOptional ) );
            return this;
        }

        /// <summary>
        /// Gets a set of assembly qualified type names that must be excluded from  
        /// registration.
        /// Note that any type appearing in <see cref="EngineConfiguration.GlobalExcludedTypes"/> will also
        /// be excluded.
        /// </summary>
        public HashSet<string> ExcludedTypes { get; }

        /// <summary>
        /// Gets the BinPath specific aspect configurations.
        /// </summary>
        public IReadOnlyCollection<BinPathAspectConfiguration> Aspects => _aspects.Values;

        /// <summary>
        /// Finds an existing aspect or returns null.
        /// </summary>
        /// <param name="name">The aspect name.</param>
        /// <returns>The aspect or null.</returns>
        public BinPathAspectConfiguration? FindAspect( string name ) => _aspects.GetValueOrDefault( name );

        /// <summary>
        /// Finds an existing aspect or returns null.
        /// <para>
        /// Whether <typeparamref name="T"/> is a <see cref="MultipleBinPathAspectConfiguration{TSelf}"/> or not
        /// doesn't matter: if at least one exists, it will be returned (with its <see cref="MultipleBinPathAspectConfiguration{TSelf}.OtherConfigurations"/>).
        /// </para>
        /// </summary>
        /// <returns>The aspect or null.</returns>
        public T? FindAspect<T>() where T : BinPathAspectConfiguration => _aspects.Values.OfType<T>().SingleOrDefault();

        /// <summary>
        /// Ensures that an aspect is registered in <see cref="Aspects"/>.
        /// <para>
        /// Whether <typeparamref name="T"/> is a <see cref="MultipleBinPathAspectConfiguration{TSelf}"/> or not
        /// doesn't matter: if at least one exists, it will be returned (with its <see cref="MultipleBinPathAspectConfiguration{TSelf}.OtherConfigurations"/>).
        /// </para>
        /// </summary>
        /// <typeparam name="T">The aspect type.</typeparam>
        /// <returns>The found or created aspect.</returns>
        public T EnsureAspect<T>() where T : BinPathAspectConfiguration, new()
        {
            T? a = FindAspect<T>();
            if( a == null )
            {
                AddAspect( a = new T() );
            }
            return a;
        }

        /// <summary>
        /// Adds an aspect to these <see cref="Aspects"/>.
        /// <para>
        /// No existing aspect with the same type must exist (except if it is a <see cref="MultipleBinPathAspectConfiguration{TSelf}"/>), the
        /// aspect must not belong to another configuration and the engine aspect must exist in the <see cref="EngineConfiguration.Aspects"/>
        /// otherwise an <see cref="ArgumentException"/> is thrown.
        /// </para>
        /// </summary>
        /// <param name="aspect">An aspect configuration to add.</param>
        public void AddAspect( BinPathAspectConfiguration aspect )
        {
            Throw.CheckNotNullArgument( aspect );
            if( aspect.Owner == this ) return;

            Throw.CheckArgument( aspect.Owner is null );

            EngineAspectConfiguration? engineAspect = Owner?.FindAspect( aspect.AspectName );
            if( Owner != null && engineAspect == null )
            {
                Throw.ArgumentException( nameof( aspect ), $"Unable to add the BinPath aspect configuration. The aspect '{aspect.AspectName}' must exist in the EngineConfiguration.Aspects." );
            }
            if( _aspects.TryGetValue( aspect.AspectName, out var existing ) )
            {
                if( existing is MultipleBinPathAspectConfiguration multi )
                {
                    if( aspect is not MultipleBinPathAspectConfiguration multiConf
                        || !multi.GetType().IsAssignableFrom( multiConf.GetType() ) )
                    {
                        Throw.ArgumentException( nameof( aspect ), $"Aspect configuration named '{aspect.AspectName}' (Type: {aspect.GetType().ToCSharpName()}) is not compatible withe existing type '{multi.GetType().ToCSharpName()}'." );
                    }
                    else
                    {
                        multi.DoAddOtherConfiguration( multiConf );
                    }
                }
                else
                {
                    Throw.ArgumentException( nameof( aspect ), $"An aspect configuration with the same type name '{aspect.AspectName}' already exists and is not a MultipleBinPathAspectConfiguration." );
                }
            }
            else
            {
                aspect.Bind( this, engineAspect );
                _aspects.Add( aspect.AspectName, aspect );
            }
        }

        /// <summary>
        /// Removes an aspect from <see cref="Aspects"/>. Does nothing if the <paramref name="aspect"/>
        /// does not belong to this configuration.
        /// </summary>
        /// <param name="aspect">An aspect configuration to remove.</param>
        public void RemoveAspect( BinPathAspectConfiguration aspect )
        {
            Throw.CheckNotNullArgument( aspect );
            if( aspect.Owner == this )
            {
                aspect.HandleOwnRemove( _aspects );
            }
        }

        /// <summary>
        /// Removes an aspect from <see cref="Aspects"/>. Does nothing if no <paramref name="name"/>
        /// aspect belong to this configuration.
        /// </summary>
        /// <param name="name">Name of the aspect configuration to remove.</param>
        public void RemoveAspect( string name )
        {
            var c = _aspects.GetValueOrDefault( name );
            if( c != null )
            {
                c.Bind( null, null );
                _aspects.Remove( name );
            }
        }
    }

}
