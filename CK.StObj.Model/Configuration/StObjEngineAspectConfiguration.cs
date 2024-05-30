using CK.Core;
using System.Xml.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Base class for configuration of a Engine Aspect.
    /// Aspect configuration must have a deserialization constructor that takes a XElement.
    /// It is highly recommended to support a <see cref="StObjEngineConfiguration.xVersion"/> attribute
    /// to ease and secure any configuration evolution. 
    /// </summary>
    public abstract class StObjEngineAspectConfiguration
    {
        readonly string _name;

        protected StObjEngineAspectConfiguration()
        {
            Throw.DebugAssert( "AspectConfiguration".Length == 19 );
            var n = GetType().Name;
            if( n.Length <= 19 || !n.EndsWith( "AspectConfiguration" ) )
            {
                Throw.InvalidDataException( $"Invalid an aspect configuration type name '{GetType().Name}': it must end with \"AspectConfiguration\"." );
            }
            _name = n.Substring( 0, n.Length - 19 );
        }

        /// <summary>
        /// Gets this aspect name: this is the type name without the "AspectConfiguration" suffix.
        /// </summary>
        public string Name => _name;

        /// <summary>
        /// Gets the fully qualified name of the type on the Engine side that implements this aspect.
        /// </summary>
        public abstract string AspectType { get; }

        /// <summary>
        /// Must serialize its content in the provided <see cref="XElement"/> and returns it.
        /// The dedicated constructor will be able to read this element back.
        /// Note that a Type attribute (that contains this aspect configuration Type name) is automatically injected.
        /// </summary>
        /// <param name="e">The element to populate.</param>
        /// <returns>The <paramref name="e"/> element.</returns>
        public abstract XElement SerializeXml( XElement e );

        /// <summary>
        /// Factory method for <see cref="BinPathConfiguration.Aspects"/>.
        /// By default, this aspect doesn't implement BinPath specific configuration: a <see cref="System.IO.InvalidDataException"/> is thrown.
        /// </summary>
        /// <param name="e">The element to parse.</param>
        /// <returns>The BinPath specific configuration.</returns>
        public virtual BinPathAspectConfiguration CreateBinPathConfiguration( XElement e )
        {
            return Throw.InvalidDataException<BinPathAspectConfiguration>( $"Aspect '{Name}' has no BinPath specific configuration." );
        }
    }
}
