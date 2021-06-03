using CK.Core;

namespace CK.Setup.Json
{
    /// <summary>
    /// The <see cref="JsonSerializationCodeGen.TypeInfoConfigurationRequired"/> is raised when a <see cref="JsonTypeInfo"/>
    /// has not been configured yet.
    /// </summary>
    public class TypeInfoConfigurationRequiredEventArg : EventMonitoredArgs
    {
        /// <summary>
        /// Initializes a new <see cref="TypeInfoConfigurationRequiredEventArg"/>.
        /// </summary>
        /// <param name="monitor">The monitor that event handlers should use.</param>
        /// <param name="c">The Json code generator context.</param>
        public TypeInfoConfigurationRequiredEventArg( IActivityMonitor monitor, JsonSerializationCodeGen c, JsonTypeInfo typeToConfigure )
            : base( monitor )
        {
            JsonCodeGen = c;
            TypeToConfigure = typeToConfigure;
        }

        /// <summary>
        /// Gets the Json code generator context. 
        /// </summary>
        public JsonSerializationCodeGen JsonCodeGen { get; }

        /// <summary>
        /// Gets the required type.
        /// </summary>
        public JsonTypeInfo TypeToConfigure { get; }
    }

}
