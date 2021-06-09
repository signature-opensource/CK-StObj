using CK.Core;
using System;

namespace CK.Setup.Json
{
    /// <summary>
    /// The <see cref="JsonSerializationCodeGen.TypeInfoRequired"/> is raised when a type
    /// is required to be serializable.
    /// </summary>
    public class TypeInfoRequiredEventArg : EventMonitoredArgs
    {
        /// <summary>
        /// Initializes a new <see cref="TypeInfoRequiredEventArg"/>.
        /// </summary>
        /// <param name="monitor">The monitor that event handlers should use.</param>
        /// <param name="c">The Json code generator context.</param>
        /// <param name="requiredType">The type for which Json serialization code is required.</param>
        public TypeInfoRequiredEventArg( IActivityMonitor monitor, JsonSerializationCodeGen c, Type requiredType )
            : base( monitor )
        {
            JsonCodeGen = c;
            RequiredType = requiredType;
        }

        /// <summary>
        /// Gets the Json code generator context. 
        /// </summary>
        public JsonSerializationCodeGen JsonCodeGen { get; }

        /// <summary>
        /// Gets the required type.
        /// </summary>
        public Type RequiredType { get; }
    }
}
