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
        /// Since all union types are non-nullable by design, all <see cref="JsonCodeGenHandler.IsNullable"/> are false.
        /// </summary>
        IReadOnlyList<JsonCodeGenHandler> AllHandlers { get; }

        /// <summary>
        /// Gets the non-nullable handlers that must be used to read an incoming value in ECMAStandard mode.
        /// These handlers have a true <see cref="JsonCodeGenHandler.HasECMAScriptStandardJsonName"/>
        /// and their <see cref="JsonCodeGenHandler.ECMAScriptStandardJsonName"/> is unique in this list.
        /// This is empty if <see cref="IPocoJsonInfo.IsECMAStandardCompliant"/> is false or if
        /// no specific handlers are needed in ECMAStandard mode.
        /// </summary>
        IReadOnlyList<JsonCodeGenHandler> ECMAStandardHandlers { get; }

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
