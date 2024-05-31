using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

namespace CK.Setup
{
    public sealed partial class EngineConfiguration
    {
        /// <summary>
        /// Initializes a new empty configuration.
        /// </summary>
        public EngineConfiguration()
        {
            _namedAspects = new Dictionary<string, EngineAspectConfiguration>();
            _aspects = new List<EngineAspectConfiguration>();
            _binPaths = new List<BinPathConfiguration>();
            AddFirstBinPath();
            GlobalExcludedTypes = new HashSet<string>();
        }

        private void AddFirstBinPath()
        {
            var first = new BinPathConfiguration();
            first.Owner = this;
            _binPaths.Add( first );
        }

        /// <summary>
        /// Initializes a new <see cref="EngineConfiguration"/> from a <see cref="XElement"/>.
        /// </summary>
        /// <param name="e">The xml element.</param>
        public EngineConfiguration( XElement e )
        {
            Throw.CheckNotNullArgument( e );

            // Global options.
            BasePath = (string?)e.Element( xBasePath );
            GeneratedAssemblyName = (string?)e.Element( xGeneratedAssemblyName );
            TraceDependencySorterInput = (bool?)e.Element( xTraceDependencySorterInput ) ?? false;
            TraceDependencySorterOutput = (bool?)e.Element( xTraceDependencySorterOutput ) ?? false;
            RevertOrderingNames = (bool?)e.Element( xRevertOrderingNames ) ?? false;
            InformationalVersion = (string?)e.Element( xInformationalVersion );
            var sha1 = (string?)e.Element( xBaseSHA1 );
            BaseSHA1 = sha1 != null ? SHA1Value.Parse( sha1 ) : SHA1Value.Zero;
            ForceRun = (bool?)e.Element( xForceRun ) ?? false;
            GlobalExcludedTypes = new HashSet<string>( FromXml( e, xGlobalExcludedTypes, xType ) );

            // Aspects.
            _aspects = new List<EngineAspectConfiguration>();
            _namedAspects = new Dictionary<string, EngineAspectConfiguration>();
            foreach( var a in e.Elements( xAspect ) )
            {
                string type = (string)a.AttributeRequired( xType );
                Type? tAspect = SimpleTypeFinder.WeakResolver( type, true );
                Debug.Assert( tAspect != null );
                EngineAspectConfiguration aspect = (EngineAspectConfiguration)Activator.CreateInstance( tAspect, a )!;
                if( _namedAspects.ContainsKey( aspect.AspectName ) )
                {
                    Throw.InvalidDataException( $"Duplicate aspect configuration found for '{aspect.AspectName}': at most one configuration aspect per type is allowed." );
                }
                _aspects.Add( aspect );
                _namedAspects.Add( aspect.AspectName, aspect );
            }

            // BinPaths.
            _binPaths = e.Elements( xBinPaths ).Elements( xBinPath ).Select( e => new BinPathConfiguration( this, e, _namedAspects ) ).ToList();
            if( _binPaths.Count == 0 ) AddFirstBinPath();

        }

        /// <summary>
        /// Serializes its content as a <see cref="XElement"/> and returns it.
        /// The <see cref="EngineConfiguration"/> constructor will be able to read this element back.
        /// Note that this Xml can also be read as a CKSetup SetupConfiguration (in Shared Configuration Mode).
        /// </summary>
        /// <returns>The Xml element.</returns>
        public XElement ToXml()
        {
            static string CleanName( Type t )
            {
                SimpleTypeFinder.WeakenAssemblyQualifiedName( t.AssemblyQualifiedName!, out string weaken );
                return weaken;
            }
            return new XElement( xConfigurationRoot,
                        new XComment( "Please see https://github.com/signature-opensource/CK-StObj/blob/master/CK.StObj.Model/Configuration/StObjEngineConfiguration.cs for documentation." ),
                        !BasePath.IsEmptyPath ? new XElement( xBasePath, BasePath ) : null,
                        GeneratedAssemblyName != StObjContextRoot.GeneratedAssemblyName ? new XElement( xGeneratedAssemblyName, GeneratedAssemblyName ) : null,
                        TraceDependencySorterInput ? new XElement( xTraceDependencySorterInput, true ) : null,
                        TraceDependencySorterOutput ? new XElement( xTraceDependencySorterOutput, true ) : null,
                        RevertOrderingNames ? new XElement( xRevertOrderingNames, true ) : null,
                        InformationalVersion != null ? new XElement( xInformationalVersion, InformationalVersion ) : null,
                        !BaseSHA1.IsZero ? new XElement( xBaseSHA1, BaseSHA1.ToString() ) : null,
                        ForceRun ? new XElement( xForceRun, true ) : null,
                        ToXml( xGlobalExcludedTypes, xType, GlobalExcludedTypes ),
                        Aspects.Select( a => a.SerializeXml( new XElement( xAspect, new XAttribute( xType, CleanName( a.GetType() ) ) ) ) ),
                        new XComment( "BinPaths: please see https://github.com/signature-opensource/CK-StObj/blob/master/CK.StObj.Model/Configuration/BinPathConfiguration.cs for documentation." ),
                        new XElement( xBinPaths, BinPaths.Select( f => f.ToXml() ) ) );
        }

        static internal XElement ToXml( XName names, XName name, IEnumerable<string> strings )
        {
            return new XElement( names, strings.Select( n => new XElement( name, n ) ) );
        }

        static internal IEnumerable<string> FromXml( XElement e, XName names, XName name )
        {
            return e.Elements( names ).Elements( name ).Select( c => (string?)c.Attribute( xName ) ?? c.Value );
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

        /// <summary>
        /// An Array element or attribute name.
        /// </summary>
        static public readonly XName xArray = XNamespace.None + "Array";

        #endregion

    }
}