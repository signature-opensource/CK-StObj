//using System;
//using System.Collections.Generic;
//using System.Text;
//using System.Xml.Linq;

//namespace CK.Setup
//{
//    /// <summary>
//    /// Configures Json generation.
//    /// Each <see cref="BinPathConfiguration"/> that requires Json serialization code to be generated must
//    /// contain a &lt;JsonSerialization&gt; element.
//    /// </summary>
//    public class JsonSerializationAspectConfiguration : IStObjEngineAspectConfiguration
//    {
//        /// <summary>
//        /// Initializes a new default configuration.
//        /// </summary>
//        public JsonSerializationAspectConfiguration()
//        {
//        }

//        /// <summary>
//        /// Initializes a new configuration from a Xml element.
//        /// </summary>
//        /// <param name="e"></param>
//        public JsonSerializationAspectConfiguration( XElement e )
//        {
//        }

//        /// <summary>
//        /// Fills the given Xml element with this configuration values.
//        /// </summary>
//        /// <param name="e">The element to fill.</param>
//        /// <returns>The element.</returns>
//        public XElement SerializeXml( XElement e )
//        {
//            e.Add( new XAttribute( StObjEngineConfiguration.xVersion, "1" ) );
//            return e;
//        }

//        /// <summary>
//        /// Gets the "CK.Setup.JsonSerializationAspect, CK.StObj.Runtime" assembly qualified name.
//        /// </summary>
//        public string AspectType => "CK.Setup.JsonSerializationAspect, CK.StObj.Runtime";
//    }
//}
