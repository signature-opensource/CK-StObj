using System.Collections.Generic;

namespace CK.Setup.Json
{
    /// <summary>
    /// Captures the Json handler (or handlers for a union type) used to read and write this <see cref="PropertyInfo"/>.
    /// </summary>
    public interface IPocoJsonPropertyInfo
    {
        /// <summary>
        /// Gets the handlers that must be used to write the value.
        /// These are also the handlers that must be used to read an incoming value in ECMASafe mode.
        /// </summary>
        IReadOnlyList<IJsonCodeGenHandler> AllHandlers { get; }

        /// <summary>
        /// Gets the handlers that must be used to read an incoming value in ECMAStandard mode.
        /// These handlers have a true <see cref="IJsonCodeGenHandler.HasECMAScriptStandardJsonName"/>
        /// and their <see cref="IJsonCodeGenHandler.ECMAScriptStandardJsonName"/> is unique in this list.
        /// This is empty if <see cref="IPocoJsonInfo.IsECMAStandardCompliant"/> is false or if
        /// no specific handlers are needed in ECMAStandard mode.
        /// </summary>
        IReadOnlyList<IJsonCodeGenHandler> ECMAStandardHandlers { get; }

        /// <summary>
        /// Gets whether this property has more than one handler.
        /// </summary>
        bool IsJsonUnionType { get; }

        /// <summary>
        /// Gets the Poco json info.
        /// </summary>
        IPocoJsonInfo PocoJsonInfo { get; }

        /// <summary>
        /// Gets the Poco property info.
        /// </summary>
        IPocoPropertyInfo PropertyInfo { get; }
    }
}
