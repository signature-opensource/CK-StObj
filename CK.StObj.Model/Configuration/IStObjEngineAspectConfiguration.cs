using System.Xml.Linq;

namespace CK.Setup
{
    /// <summary>
    /// All configuration of a Engine Aspect must implement this interface.
    /// Aspect configuration must have a deserialization constructor that takes a XElement.
    /// It is highly recommended to support a <see cref="StObjEngineConfiguration.xVersion"/> attribute
    /// to ease and secure any configuration evolution. 
    /// </summary>
    public interface IStObjEngineAspectConfiguration
    {
        /// <summary>
        /// Gets the fully qualified name of the class that implements this aspect.
        /// </summary>
        string AspectType { get; }

        /// <summary>
        /// Serializes its content in the provided <see cref="XElement"/> and returns it.
        /// The dedicated constructor will be able to read this element back.
        /// Note that a Type attribute (that contains this aspect configuration Type name) is automatically injected.
        /// </summary>
        /// <param name="e">The element to populate.</param>
        /// <returns>The <paramref name="e"/> element.</returns>
        XElement SerializeXml( XElement e );

    }
}
