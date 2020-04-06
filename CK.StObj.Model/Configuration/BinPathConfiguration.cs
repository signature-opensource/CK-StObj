using CK.Core;
using CK.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Describes a folder to process and for which a <see cref="IStObjMap"/> should be generated,
    /// either in source code or as compiled Dynamic Linked Library.
    /// <para>
    /// These configuration objects are shared with CKSetup configuration: CKSetup handles only the &lt;BinPath Path="..." /&gt;
    /// element on imput (and adds a BinPath="..." attribute that contains the actual bin path folder - typically ending with /publish
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
                if( k != null ) Kind = (AutoServiceKind)Enum.Parse( typeof( AutoServiceKind ), k );
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
            /// </summary>
            public AutoServiceKind Kind { get; set; }

            /// <summary>
            /// Gets or sets whether this type is optional: if the <see cref="TypeName"/> cannot be resolved
            /// a warning is emitted.
            /// Defaults to false: by default, if the type is not found at runtime, an error is raised.
            /// </summary>
            public bool Optional { get; set; }

            /// <summary>
            /// Overridden to returne the Name - Kind and Optional value.
            /// </summary>
            /// <returns>A readable string.</returns>
            public override string ToString() => $"{Name} - {Kind} - {Optional}";
        }

        /// <summary>
        /// Initializes a new empty <see cref="BinPathConfiguration"/>.
        /// At least, the <see cref="Path"/> should be set for this BinPath to be valid.
        /// </summary>
        public BinPathConfiguration()
        {
            GenerateSourceFiles = true;
            Assemblies = new HashSet<string>();
            ExcludedTypes = new HashSet<string>();
            Types = new List<TypeConfiguration>();
        }

        /// <summary>
        /// Initializes a new <see cref="BinPathConfiguration"/> from a Xml element.
        /// </summary>
        /// <param name="e">The Xml element.</param>
        public BinPathConfiguration( XElement e )
        {
            Path = (string)e.Attribute( StObjEngineConfiguration.xPath );
            OutputPath = (string)e.Element( StObjEngineConfiguration.xOutputPath );
            SkipCompilation = (bool?)e.Element( StObjEngineConfiguration.xSkipCompilation ) ?? false;
            GenerateSourceFiles = (bool?)e.Element( StObjEngineConfiguration.xGenerateSourceFiles ) ?? true;

            Assemblies = new HashSet<string>( StObjEngineConfiguration.FromXml( e, StObjEngineConfiguration.xAssemblies, StObjEngineConfiguration.xAssembly ) );
            ExcludedTypes = new HashSet<string>( StObjEngineConfiguration.FromXml( e, StObjEngineConfiguration.xExcludedTypes, StObjEngineConfiguration.xType ) );

            Types = e.Elements( StObjEngineConfiguration.xTypes ).Elements( StObjEngineConfiguration.xType ).Select( c => new TypeConfiguration( c ) ).ToList();
        }

        /// <summary>
        /// Creates a xml element from this <see cref="BinPathConfiguration"/>.
        /// </summary>
        /// <returns>A new element.</returns>
        public XElement ToXml()
        {
            return new XElement( StObjEngineConfiguration.xBinPath,
                                    new XAttribute( StObjEngineConfiguration.xPath, Path ),
                                    !OutputPath.IsEmptyPath ? new XElement( StObjEngineConfiguration.xOutputPath, OutputPath ) : null,
                                    SkipCompilation ? new XElement( StObjEngineConfiguration.xSkipCompilation, true ) : null,
                                    GenerateSourceFiles ? null : new XElement( StObjEngineConfiguration.xGenerateSourceFiles, false ),
                                    StObjEngineConfiguration.ToXml( StObjEngineConfiguration.xAssemblies, StObjEngineConfiguration.xAssembly, Assemblies ),
                                    StObjEngineConfiguration.ToXml( StObjEngineConfiguration.xExcludedTypes, StObjEngineConfiguration.xType, ExcludedTypes ),
                                    Types.Select( t => new XElement( StObjEngineConfiguration.xType,
                                                            new XAttribute( StObjEngineConfiguration.xName, t.Name ),
                                                            t.Kind != AutoServiceKind.None ? new XAttribute( StObjEngineConfiguration.xKind, t.Kind ) : null,
                                                            t.Optional ? new XAttribute( StObjEngineConfiguration.xOptional, true ) : null ) ) );
        }

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
        /// Gets or sets whether the compilation should be skipped for this folder (and compiled assembly shouldn't be
        /// copied to <see cref="OutputPath"/>).
        /// Defaults to false.
        /// </summary>
        public bool SkipCompilation { get; set; }

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

    }
}