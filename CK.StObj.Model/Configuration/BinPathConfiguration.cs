using CK.Core;
using CK.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public class BinPathConfiguration
    {
        /// <summary>
        /// Models the &lt;Type&gt; elements that are children of &lt;Types&gt;.
        /// </summary>
        public class TypeConfiguration
        {
            /// <summary>
            /// Initializes a new <see cref="TypeConfiguration"/> from a Xml element.
            /// </summary>
            /// <param name="e">The Xml element.</param>
            public TypeConfiguration( XElement e )
            {
                Name = (string)e.Attribute( StObjEngineConfiguration.xName ) ?? e.Value;
                string k = (string)e.Attribute( StObjEngineConfiguration.xKind );
                if( k != null ) Kind = (AutoServiceKind)Enum.Parse( typeof( AutoServiceKind ), k.Replace( '|', ',' ) );
                Optional = (bool?)e.Attribute( StObjEngineConfiguration.xOptional ) ?? false;
            }

            /// <summary>
            /// Initializes a new <see cref="TypeConfiguration"/>.
            /// </summary>
            /// <param name="name">Name of the type.</param>
            /// <param name="kind">Kind.</param>
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
            /// This is used as the equality key when configurations are grouped into equivalency classes.
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
            AspectConfigurations = new List<XElement>();
        }

        /// <summary>
        /// Initializes a new <see cref="BinPathConfiguration"/> from a Xml element.
        /// All <see cref="AspectConfigurations"/> (extra elements) are cloned.
        /// </summary>
        /// <param name="e">The Xml element.</param>
        public BinPathConfiguration( XElement e )
        {
            Name = (string?)e.Attribute( StObjEngineConfiguration.xName );
            Path = (string)e.Attribute( StObjEngineConfiguration.xPath );
            OutputPath = (string)e.Element( StObjEngineConfiguration.xOutputPath );
            ProjectPath = (string)e.Element( StObjEngineConfiguration.xProjectPath );

            if( e.Element( "SkipCompilation" ) != null )
            {
                throw new XmlException( @"Element SkipCompilation must be replaced with CompileOption that can be be ""None"", ""Parse"" or ""Compile"". It defaults to ""None""." );
            }
            CompileOption = e.Element( StObjEngineConfiguration.xCompileOption )?.Value.ToLowerInvariant() switch
            {
                null => CompileOption.None,
                "none" => CompileOption.None,
                "parse" => CompileOption.Parse,
                "compile" => CompileOption.Compile,
                _ => throw new XmlException( @"Expected CompileOption to be ""None"", ""Parse"" or ""Compile""." )
            };

            GenerateSourceFiles = (bool?)e.Element( StObjEngineConfiguration.xGenerateSourceFiles ) ?? true;

            Assemblies = new HashSet<string>( StObjEngineConfiguration.FromXml( e, StObjEngineConfiguration.xAssemblies, StObjEngineConfiguration.xAssembly ) );
            ExcludedTypes = new HashSet<string>( StObjEngineConfiguration.FromXml( e, StObjEngineConfiguration.xExcludedTypes, StObjEngineConfiguration.xType ) );

            Types = e.Elements( StObjEngineConfiguration.xTypes ).Elements( StObjEngineConfiguration.xType ).Select( c => new TypeConfiguration( c ) ).ToList();

            AspectConfigurations = e.Elements().Where( e => e.Name != StObjEngineConfiguration.xTypes
                                                            && e.Name != StObjEngineConfiguration.xExcludedTypes
                                                            && e.Name != StObjEngineConfiguration.xAssemblies
                                                            && e.Name != StObjEngineConfiguration.xGenerateSourceFiles
                                                            && e.Name != StObjEngineConfiguration.xCompileOption
                                                            && e.Name != StObjEngineConfiguration.xProjectPath
                                                            && e.Name != StObjEngineConfiguration.xOutputPath
                                                            && e.Name != StObjEngineConfiguration.xPath
                                                            && e.Name != StObjEngineConfiguration.xName )
                                               .Select( e => new XElement( e ) )
                                               .ToList();
        }

        /// <summary>
        /// Creates a xml element from this <see cref="BinPathConfiguration"/>.
        /// <see cref="AspectConfigurations"/> are cloned.
        /// </summary>
        /// <returns>A new element.</returns>
        public XElement ToXml()
        {
            return new XElement( StObjEngineConfiguration.xBinPath,
                                    String.IsNullOrWhiteSpace( Name ) ? null : new XAttribute( StObjEngineConfiguration.xName, Name ),
                                    new XAttribute( StObjEngineConfiguration.xPath, Path ),
                                    !OutputPath.IsEmptyPath ? new XElement( StObjEngineConfiguration.xOutputPath, OutputPath ) : null,
                                    !ProjectPath.IsEmptyPath ? new XElement( StObjEngineConfiguration.xProjectPath, ProjectPath ) : null,
                                    new XElement( StObjEngineConfiguration.xCompileOption, CompileOption.ToString() ),
                                    GenerateSourceFiles ? null : new XElement( StObjEngineConfiguration.xGenerateSourceFiles, false ),
                                    StObjEngineConfiguration.ToXml( StObjEngineConfiguration.xAssemblies, StObjEngineConfiguration.xAssembly, Assemblies ),
                                    StObjEngineConfiguration.ToXml( StObjEngineConfiguration.xExcludedTypes, StObjEngineConfiguration.xType, ExcludedTypes ),
                                    new XElement( StObjEngineConfiguration.xTypes,
                                                    Types.Select( t => new XElement( StObjEngineConfiguration.xType,
                                                                            new XAttribute( StObjEngineConfiguration.xName, t.Name ),
                                                                            t.Kind != AutoServiceKind.None ? new XAttribute( StObjEngineConfiguration.xKind, t.Kind ) : null,
                                                                            t.Optional ? new XAttribute( StObjEngineConfiguration.xOptional, true ) : null ) ) ),
                                    AspectConfigurations.Select( e => new XElement( e ) ) );
        }

        /// <summary>
        /// Gets or sets the name that uniquely identifies this configuration among the others.
        /// When null, an automatically numbered name is generated: only the unified bin path has an empty name.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the path of the directory to setup (this property is shared with CKSetup configuration).
        /// It can be relative: it will be combined to the <see cref="StObjEngineConfiguration.BasePath"/>.
        /// </summary>
        public NormalizedPath Path { get; set; }

        /// <summary>
        /// Gets or sets an optional target (output) directory where generated files (assembly and/or sources)
        /// must be copied. When <see cref="NormalizedPath.IsEmptyPath"/>, this <see cref="Path"/> is used.
        /// </summary>
        public NormalizedPath OutputPath { get; set; }

        /// <summary>
        /// Gets or sets an optional target (output) directory for source files.
        /// When not <see cref="NormalizedPath.IsEmptyPath"/>, a "$StObjGen" folder is created and
        /// the source files are moved from the <see cref="OutputPath"/> to this one and, for ".cs" files,
        /// they are renamed into standard names "G0.cs", "G1.cs", etc. (even if currently only one file
        /// is generated).
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
        /// Gets a set of assembly qualified type names that must be excluded from  
        /// registration.
        /// Note that any type appearing in <see cref="StObjEngineConfiguration.GlobalExcludedTypes"/> will also
        /// be excluded.
        /// </summary>
        public HashSet<string> ExcludedTypes { get; }

        /// <summary>
        /// Gets a mutable set of <see cref="XElement"/> that are configurations for aspects.
        /// Element names should match aspect's name (see <see cref="GetAspectConfiguration(string)"/>).
        /// </summary>
        public List<XElement> AspectConfigurations { get; }

        /// <summary>
        /// Helper that attempts to find an element in <see cref="AspectConfigurations"/> based on an aspect type.
        /// See <see cref="GetAspectConfiguration(string)"/>.
        /// </summary>
        /// <param name="aspect">The aspect's type.</param>
        /// <returns>The element or null.</returns>
        public XElement? GetAspectConfiguration( Type aspect ) => GetAspectConfiguration( aspect.Name );

        /// <summary>
        /// Helper that attempts to find an element in <see cref="AspectConfigurations"/> based on an aspect type.
        /// See <see cref="GetAspectConfiguration(string)"/>.
        /// </summary>
        /// <typeparam name="T">The aspect's type.</typeparam>
        /// <returns>The element or null.</returns>
        public XElement? GetAspectConfiguration<T>() => GetAspectConfiguration( typeof(T).Name );

        /// <summary>
        /// Helper that attempts to find an element in <see cref="AspectConfigurations"/> based on an aspect name:
        /// combinations of "Configurations", "Configuration", "Config" suffixes and "Aspect" substring are removed.
        /// </summary>
        /// <param name="aspectName">The name of the aspect.</param>
        /// <returns>The element or null.</returns>
        public XElement? GetAspectConfiguration( string aspectName )
        {
            XElement? e = AspectConfigurations.FirstOrDefault( e => e.Name.LocalName == aspectName );
            if( e != null ) return e;
            string? noConf = null;
            if( aspectName.EndsWith( "Configurations" ) ) noConf = aspectName.Substring( 0, aspectName.Length - 14 );
            else if( aspectName.EndsWith( "Configuration" ) ) noConf = aspectName.Substring( 0, aspectName.Length - 13 );
            else if( aspectName.EndsWith( "Config" ) ) noConf = aspectName.Substring( 0, aspectName.Length - 6 );
            if( noConf != null )
            {
                e = AspectConfigurations.FirstOrDefault( e => e.Name.LocalName == noConf );
                if( e != null ) return e;
            }
            aspectName = aspectName.Replace( "Aspect", "" );
            e = AspectConfigurations.FirstOrDefault( e => e.Name.LocalName == aspectName );
            if( e != null ) return e;
            if( noConf != null ) noConf = noConf.Replace( "Aspect", "" );
            e = AspectConfigurations.FirstOrDefault( e => e.Name.LocalName == noConf );
            return e;
        }
    }
}
