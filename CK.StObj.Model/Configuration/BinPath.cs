using CK.Core;
using CK.Text;
using System.Collections.Generic;
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
    public class BinPath
    {
        /// <summary>
        /// Initializes a new empty <see cref="BinPath"/>.
        /// At least, the <see cref="Path"/> should be set for this BinPath to be valid.
        /// </summary>
        public BinPath()
        {
            GenerateSourceFiles = true;
            Assemblies = new HashSet<string>();
            Types = new HashSet<string>();
            ExcludedTypes = new HashSet<string>();
            ExternalSingletonTypes = new HashSet<string>();
            ExternalScopedTypes = new HashSet<string>();
        }

        /// <summary>
        /// Initializes a new <see cref="BinPath"/> from a Xml element.
        /// </summary>
        public BinPath( XElement e )
        {
            Path = (string)e.Attribute( StObjEngineConfiguration.xPath );
            OutputPath = (string)e.Element( StObjEngineConfiguration.xOutputPath );
            SkipCompilation = (bool?)e.Element( StObjEngineConfiguration.xSkipCompilation ) ?? false;
            GenerateSourceFiles = (bool?)e.Element( StObjEngineConfiguration.xGenerateSourceFiles ) ?? true;

            Assemblies = new HashSet<string>( StObjEngineConfiguration.FromXml( e, StObjEngineConfiguration.xAssemblies, StObjEngineConfiguration.xAssembly ) );
            Types = new HashSet<string>( StObjEngineConfiguration.FromXml( e, StObjEngineConfiguration.xTypes, StObjEngineConfiguration.xType ) );
            ExternalSingletonTypes = new HashSet<string>( StObjEngineConfiguration.FromXml( e, StObjEngineConfiguration.xExternalSingletonTypes, StObjEngineConfiguration.xType ) );
            ExternalScopedTypes = new HashSet<string>( StObjEngineConfiguration.FromXml( e, StObjEngineConfiguration.xExternalScopedTypes, StObjEngineConfiguration.xType ) );
            ExcludedTypes = new HashSet<string>( StObjEngineConfiguration.FromXml( e, StObjEngineConfiguration.xExcludedTypes, StObjEngineConfiguration.xType ) );
        }

        /// <summary>
        /// Creates a xml element from this <see cref="BinPath"/>.
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
                                    StObjEngineConfiguration.ToXml( StObjEngineConfiguration.xTypes, StObjEngineConfiguration.xType, Types ),
                                    StObjEngineConfiguration.ToXml( StObjEngineConfiguration.xExternalSingletonTypes, StObjEngineConfiguration.xType, ExternalSingletonTypes ),
                                    StObjEngineConfiguration.ToXml( StObjEngineConfiguration.xExternalScopedTypes, StObjEngineConfiguration.xType, ExternalScopedTypes ),
                                    StObjEngineConfiguration.ToXml( StObjEngineConfiguration.xExcludedTypes, StObjEngineConfiguration.xType, ExcludedTypes ) );
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
        /// Gets a set of assembly qualified type names that must be explicitly registered 
        /// regardless of <see cref="Assemblies"/>.
        /// </summary>
        public HashSet<string> Types { get; }

        /// <summary>
        /// Gets a set of assembly qualified type names that must be excluded from  
        /// registration.
        /// Note that any type appearing in <see cref="StObjEngineConfiguration.GlobalExcludedTypes"/> will also
        /// be excluded.
        /// </summary>
        public HashSet<string> ExcludedTypes { get; }

        /// <summary>
        /// Gets a set of assembly qualified type names that are known to be singletons. 
        /// </summary>
        public HashSet<string> ExternalSingletonTypes { get; }

        /// <summary>
        /// Gets a set of assembly qualified type names that are known to be scoped. 
        /// </summary>
        public HashSet<string> ExternalScopedTypes { get; }

    }
}
