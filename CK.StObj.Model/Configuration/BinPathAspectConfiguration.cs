using CK.Core;
using System.Xml.Linq;

namespace CK.Setup
{
    /// <summary>
    /// BinPath specific configuration of a <see cref="StObjEngineAspectConfiguration"/>.
    /// Not all aspects need a per BinPath configuration.
    /// </summary>
    public abstract class BinPathAspectConfiguration
    {
        readonly string _name;

        /// <summary>
        /// Initializes an empty BinPath aspect configuration.
        /// </summary>
        protected BinPathAspectConfiguration()
        {
            Throw.DebugAssert( "BinPathAspectConfiguration".Length == 26 );
            var n = GetType().Name;
            if( n.Length <= 26 || !n.EndsWith( "BinPathAspectConfiguration" ) )
            {
                Throw.CKException( $"Invalid BinPath aspect configuration type name '{GetType().Name}': it must end with \"BinPathAspectConfiguration\"." );
            }
            _name = n.Substring( 0, n.Length - 26 );
        }

        /// <summary>
        /// Gets the aspect name: this is the type name without the "BinPathAspectConfiguration" suffix.
        /// </summary>
        public string Name => _name;

        /// <summary>
        /// Gets the aspect configuration if this instance belongs to a <see cref="BinPathConfiguration.Aspects"/>,
        /// null otherwise.
        /// </summary>
        public StObjEngineAspectConfiguration? AspectConfiguration { get; internal set; }

        /// <summary>
        /// Gets the <see cref="BinPathConfiguration"/> to which this aspect belongs.
        /// </summary>
        public BinPathConfiguration? Owner { get; internal set; }

        /// <summary>
        /// Must reset this instance from the xml element.
        /// </summary>
        /// <param name="e">The xml element to read.</param>
        public abstract void InitializeFrom( XElement e );

        /// <summary>
        /// Serialize this instance in xml.
        /// </summary>
        /// <returns>The xml element.</returns>
        public XElement ToXml()
        {
            var e = new XElement( _name );
            WriteXml(  e );
            return e;
        }

        /// <summary>
        /// Must serialize its content in the provided <see cref="XElement"/> and returns it.
        /// </summary>
        /// <param name="e">The element to populate.</param>
        protected abstract void WriteXml( XElement e );
    }

}
