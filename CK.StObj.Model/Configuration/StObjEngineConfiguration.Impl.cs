using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace CK.Setup
{
    public sealed partial class StObjEngineConfiguration 
    {
        string _generatedAssemblyName;

        /// <summary>
        /// Initializes a new empty configuration.
        /// </summary>
        public StObjEngineConfiguration()
        {
            Aspects = new List<IStObjEngineAspectConfiguration>();
            BinPaths = new List<BinPath>();
            GlobalExcludedTypes = new HashSet<string>();
        }

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
        /// The GenerateSourceFiles element name.
        /// </summary>
        static public readonly XName xGenerateSourceFiles = XNamespace.None + "GenerateSourceFiles";

        /// <summary>
        /// The SkipCompilation element name.
        /// </summary>
        static public readonly XName xSkipCompilation = XNamespace.None + "SkipCompilation";

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
        /// The InformationalVersion element name.
        /// </summary>
        static public readonly XName xInformationalVersion = XNamespace.None + "InformationalVersion";

        #endregion

        /// <summary>
        /// Initializes a new <see cref="StObjEngineConfiguration"/> from a <see cref="XElement"/>.
        /// </summary>
        /// <param name="e">The xml element.</param>
        public StObjEngineConfiguration( XElement e )
        {
            // Global options.
            BasePath = (string)e.Element( xBasePath );
            GeneratedAssemblyName = (string)e.Element( xGeneratedAssemblyName );
            TraceDependencySorterInput = (bool?)e.Element( xTraceDependencySorterInput ) ?? false;
            TraceDependencySorterOutput = (bool?)e.Element( xTraceDependencySorterOutput ) ?? false;
            RevertOrderingNames = (bool?)e.Element( xRevertOrderingNames ) ?? false;
            InformationalVersion = (string)e.Element( xInformationalVersion );
 
            GlobalExcludedTypes = new HashSet<string>( FromXml( e, xGlobalExcludedTypes, xType ) );

            // BinPaths.
            BinPaths = e.Elements( xBinPaths ).Elements( xBinPath ).Select( f => new BinPath( f ) ).ToList();

            // Aspects.
            Aspects = new List<IStObjEngineAspectConfiguration>();
            foreach( var a in e.Elements( xAspect ) )
            {
                string type = (string)a.AttributeRequired( xType );
                Type tAspect = SimpleTypeFinder.WeakResolver( type, true );
                IStObjEngineAspectConfiguration aspect = (IStObjEngineAspectConfiguration)Activator.CreateInstance( tAspect, a );
                Aspects.Add( aspect );
            }
        }

        /// <summary>
        /// Serializes its content as a <see cref="XElement"/> and returns it.
        /// The <see cref="StObjEngineConfiguration"/> constructor will be able to read this element back.
        /// Note that this Xml can also be read by as a CKSetup SetupConfiguration (in Shared Configuration Mode).
        /// </summary>
        /// <returns>The Xml element.</returns>
        public XElement ToXml()
        {
            string CleanName( Type t )
            {
                SimpleTypeFinder.WeakenAssemblyQualifiedName( t.AssemblyQualifiedName, out string weaken );
                return weaken;
            }
            return new XElement( xConfigurationRoot,
                        new XComment( "Please see https://gitlab.com/signature-code/CK-Database/raw/develop/CK.StObj.Model/Configuration/StObjEngineConfiguration.cs for documentation." ),
                        !BasePath.IsEmptyPath ? new XElement( xBasePath, BasePath ) : null,
                        GeneratedAssemblyName != DefaultGeneratedAssemblyName ? new XElement( xGeneratedAssemblyName, GeneratedAssemblyName ) : null,
                        TraceDependencySorterInput ? new XElement( xTraceDependencySorterInput, true ) : null,
                        TraceDependencySorterOutput ? new XElement( xTraceDependencySorterOutput, true ) : null,
                        RevertOrderingNames ? new XElement( xRevertOrderingNames, true ) : null,
                        InformationalVersion != null ? new XElement( xInformationalVersion, InformationalVersion ) : null,
                        ToXml( xGlobalExcludedTypes, xType, GlobalExcludedTypes ),
                        Aspects.Select( a => a.SerializeXml( new XElement( xAspect, new XAttribute( xType, CleanName( a.GetType() ) ) ) ) ),
                        new XComment( "BinPaths: please see https://gitlab.com/signature-code/CK-Database/raw/develop/CK.StObj.Model/Configuration/BinPath.cs for documentation." ),
                        new XElement( xBinPaths, BinPaths.Select( f => f.ToXml() ) ) );
        }

        static internal XElement ToXml( XName names, XName name, IEnumerable<string> strings )
        {
            return new XElement( names, strings.Select( n => new XElement( name, n ) ) );
        }

        static internal IEnumerable<string> FromXml( XElement e, XName names, XName name )
        {
            return e.Elements( names ).Elements( name ).Select( c => c.Value );
        }

    }
}
