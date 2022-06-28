using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

namespace CK.Setup
{
    public abstract partial class StObjEngineConfiguration<TBinPath> 
    {
        string? _generatedAssemblyName;

        /// <summary>
        /// Initializes a new empty configuration.
        /// </summary>
        public StObjEngineConfiguration()
        {
            Aspects = new List<IStObjEngineAspectConfiguration>();
            BinPaths = new List<TBinPath>();
            GlobalExcludedTypes = new HashSet<string>();
        }

        /// <summary>
        /// Initializes a new <see cref="StObjEngineConfiguration"/> from a <see cref="XElement"/>.
        /// </summary>
        /// <param name="e">The xml element.</param>
        public StObjEngineConfiguration( XElement e )
        {
            // Global options.
            BasePath = (string?)e.Element( StObjEngineConfiguration.xBasePath );
            GeneratedAssemblyName = (string?)e.Element( StObjEngineConfiguration.xGeneratedAssemblyName );
            TraceDependencySorterInput = (bool?)e.Element( StObjEngineConfiguration.xTraceDependencySorterInput ) ?? false;
            TraceDependencySorterOutput = (bool?)e.Element( StObjEngineConfiguration.xTraceDependencySorterOutput ) ?? false;
            RevertOrderingNames = (bool?)e.Element( StObjEngineConfiguration.xRevertOrderingNames ) ?? false;
            InformationalVersion = (string?)e.Element( StObjEngineConfiguration.xInformationalVersion );
            var sha1 = (string?)e.Element( StObjEngineConfiguration.xBaseSHA1 );
            BaseSHA1 = sha1 != null ? SHA1Value.Parse( sha1 ) : SHA1Value.Zero;
            ForceRun = (bool?)e.Element( StObjEngineConfiguration.xForceRun ) ?? false;
            GlobalExcludedTypes = new HashSet<string>( FromXml( e, StObjEngineConfiguration.xGlobalExcludedTypes, StObjEngineConfiguration.xType ) );

            // BinPaths.
            BinPaths = e.Elements( StObjEngineConfiguration.xBinPaths ).Elements( StObjEngineConfiguration.xBinPath ).Select( CreateBinPath ).ToList();

            // Aspects.
            Aspects = new List<IStObjEngineAspectConfiguration>();
            foreach( var a in e.Elements( StObjEngineConfiguration.xAspect ) )
            {
                string type = (string)a.AttributeRequired( StObjEngineConfiguration.xType );
                Type? tAspect = SimpleTypeFinder.WeakResolver( type, true );
                Debug.Assert( tAspect != null );
                IStObjEngineAspectConfiguration aspect = (IStObjEngineAspectConfiguration)Activator.CreateInstance( tAspect, a )!;
                Aspects.Add( aspect );
            }
        }

        /// <summary>
        /// Factory methods of <typeparamref name="TBinPath"/>.
        /// </summary>
        /// <param name="e">The Xml element.</param>
        /// <returns>A configured BinPathConfiguration.</returns>
        protected abstract TBinPath CreateBinPath( XElement e );

        /// <summary>
        /// Serializes its content as a <see cref="XElement"/> and returns it.
        /// The <see cref="StObjEngineConfiguration"/> constructor will be able to read this element back.
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
            return new XElement( StObjEngineConfiguration.xConfigurationRoot,
                        new XComment( "Please see https://github.com/signature-opensource/CK-StObj/blob/master/CK.StObj.Model/Configuration/StObjEngineConfiguration.cs for documentation." ),
                        !BasePath.IsEmptyPath ? new XElement( StObjEngineConfiguration.xBasePath, BasePath ) : null,
                        GeneratedAssemblyName != StObjContextRoot.GeneratedAssemblyName ? new XElement( StObjEngineConfiguration.xGeneratedAssemblyName, GeneratedAssemblyName ) : null,
                        TraceDependencySorterInput ? new XElement( StObjEngineConfiguration.xTraceDependencySorterInput, true ) : null,
                        TraceDependencySorterOutput ? new XElement( StObjEngineConfiguration.xTraceDependencySorterOutput, true ) : null,
                        RevertOrderingNames ? new XElement( StObjEngineConfiguration.xRevertOrderingNames, true ) : null,
                        InformationalVersion != null ? new XElement( StObjEngineConfiguration.xInformationalVersion, InformationalVersion ) : null,
                        BaseSHA1.IsZero ? new XElement( StObjEngineConfiguration.xBaseSHA1, BaseSHA1.ToString() ) : null,
                        ForceRun ? new XElement( StObjEngineConfiguration.xForceRun, true ) : null,
                        ToXml( StObjEngineConfiguration.xGlobalExcludedTypes, StObjEngineConfiguration.xType, GlobalExcludedTypes ),
                        Aspects.Select( a => a.SerializeXml( new XElement( StObjEngineConfiguration.xAspect, new XAttribute( StObjEngineConfiguration.xType, CleanName( a.GetType() ) ) ) ) ),
                        new XComment( "BinPaths: please see https://github.com/signature-opensource/CK-StObj/blob/master/CK.StObj.Model/Configuration/BinPathConfiguration.cs for documentation." ),
                        new XElement( StObjEngineConfiguration.xBinPaths, BinPaths.Select( f => f.ToXml() ) ) );
        }

        static internal XElement ToXml( XName names, XName name, IEnumerable<string> strings )
        {
            return new XElement( names, strings.Select( n => new XElement( name, n ) ) );
        }

        static internal IEnumerable<string> FromXml( XElement e, XName names, XName name )
        {
            return e.Elements( names ).Elements( name ).Select( c => (string?)c.Attribute( StObjEngineConfiguration.xName ) ?? c.Value );
        }

    }
}
