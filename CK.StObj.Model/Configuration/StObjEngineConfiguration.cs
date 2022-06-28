using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Encapsulates configuration of the StObjEngine.
    /// This configuration is compatible with CKSetup SetupConfiguration object.
    /// </summary>
    public sealed class StObjEngineConfiguration : StObjEngineConfiguration<BinPathConfiguration>
    {
        /// <inheritdoc />
        public StObjEngineConfiguration()
        {
        }

        /// <inheritdoc />
        public StObjEngineConfiguration( XElement e )
            : base( e )
        {
        }

        protected override BinPathConfiguration CreateBinPath( XElement e ) => new BinPathConfiguration( e );

        #region Xml centralized names.

        /// <summary>
        /// The root configuration name is Setup (it can be anything).
        /// </summary>
        static public readonly XName xConfigurationRoot = XNamespace.None + "Setup";

        /// <summary>
        /// The version attribute name.
        /// </summary>
        static public readonly XName xVersion = XNamespace.None + "Version";

        /// <summary>
        /// The Aspect element name.
        /// </summary>
        static public readonly XName xAspect = XNamespace.None + "Aspect";

        /// <summary>
        /// The Assemblies element name.
        /// </summary>
        static public readonly XName xAssemblies = XNamespace.None + "Assemblies";

        /// <summary>
        /// The Assembly element name.
        /// </summary>
        static public readonly XName xAssembly = XNamespace.None + "Assembly";

        /// <summary>
        /// The Types element name.
        /// </summary>
        static public readonly XName xTypes = XNamespace.None + "Types";

        /// <summary>
        /// The ExternalSingletonTypes element name.
        /// </summary>
        static public readonly XName xExternalSingletonTypes = XNamespace.None + "ExternalSingletonTypes";

        /// <summary>
        /// The ExternalScopedTypes element name.
        /// </summary>
        static public readonly XName xExternalScopedTypes = XNamespace.None + "ExternalScopedTypes";

        /// <summary>
        /// The ExcludedTypes element name.
        /// </summary>
        static public readonly XName xExcludedTypes = XNamespace.None + "ExcludedTypes";

        /// <summary>
        /// The ExcludedTypes element name.
        /// </summary>
        static public readonly XName xGlobalExcludedTypes = XNamespace.None + "GlobalExcludedTypes";

        /// <summary>
        /// The AvailableStObjMapSignatures element name.
        /// </summary>
        static public readonly XName xAvailableStObjMapSignatures = XNamespace.None + "AvailableStObjMapSignatures";

        /// <summary>
        /// The Signature element name.
        /// </summary>
        static public readonly XName xSignature = XNamespace.None + "Signature";

        /// <summary>
        /// The Type element name.
        /// </summary>
        static public readonly XName xType = XNamespace.None + "Type";

        /// <summary>
        /// The BasePath element name.
        /// </summary>
        static public readonly XName xBasePath = XNamespace.None + "BasePath";

        /// <summary>
        /// The BinPath element name.
        /// </summary>
        static public readonly XName xBinPath = XNamespace.None + "BinPath";

        /// <summary>
        /// The BinPaths element name.
        /// </summary>
        static public readonly XName xBinPaths = XNamespace.None + "BinPaths";

        /// <summary>
        /// The Path element name.
        /// </summary>
        static public readonly XName xPath = XNamespace.None + "Path";

        /// <summary>
        /// The RevertOrderingNames element name.
        /// </summary>
        static public readonly XName xRevertOrderingNames = XNamespace.None + "RevertOrderingNames";

        /// <summary>
        /// The OutputPath element name.
        /// </summary>
        static public readonly XName xOutputPath = XNamespace.None + "OutputPath";

        /// <summary>
        /// The OutputPath element name.
        /// </summary>
        static public readonly XName xProjectPath = XNamespace.None + "ProjectPath";

        /// <summary>
        /// The GenerateSourceFiles element name.
        /// </summary>
        static public readonly XName xGenerateSourceFiles = XNamespace.None + "GenerateSourceFiles";

        /// <summary>
        /// The CompileOption element name.
        /// </summary>
        static public readonly XName xCompileOption = XNamespace.None + "CompileOption";

        /// <summary>
        /// The TraceDependencySorterInput element name.
        /// </summary>
        static public readonly XName xTraceDependencySorterInput = XNamespace.None + "TraceDependencySorterInput";

        /// <summary>
        /// The TraceDependencySorterOutput element name.
        /// </summary>
        static public readonly XName xTraceDependencySorterOutput = XNamespace.None + "TraceDependencySorterOutput";

        /// <summary>
        /// The GeneratedAssemblyName element name.
        /// </summary>
        static public readonly XName xGeneratedAssemblyName = XNamespace.None + "GeneratedAssemblyName";

        /// <summary>
        /// The attribute Name.
        /// </summary>
        static public readonly XName xName = XNamespace.None + "Name";

        /// <summary>
        /// The attribute Kind.
        /// </summary>
        static public readonly XName xKind = XNamespace.None + "Kind";

        /// <summary>
        /// The attribute Optional.
        /// </summary>
        static public readonly XName xOptional = XNamespace.None + "Optional";

        /// <summary>
        /// The InformationalVersion element name.
        /// </summary>
        static public readonly XName xInformationalVersion = XNamespace.None + "InformationalVersion";

        /// <summary>
        /// The BaseSHA1 element name.
        /// </summary>
        static public readonly XName xBaseSHA1 = XNamespace.None + "BaseSHA1";

        /// <summary>
        /// The ForceRun element name.
        /// </summary>
        static public readonly XName xForceRun = XNamespace.None + "ForceRun";

        #endregion

    }
}
