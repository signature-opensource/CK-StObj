using CK.Core;
using System.Collections.Generic;

namespace CK.Setup.Json
{
    /// <summary>
    /// Captures Json information for a <see cref="IPocoRootInfo"/>.
    /// This is available if the CK.Poco.Json package is installed and
    /// the generation succeeds on the <see cref="IPocoRootInfo"/> by
    /// using <see cref="IAnnotationSet.Annotation{IPocoJsonInfo}"/>.
    /// </summary>
    public interface IPocoJsonInfo
    {
        
        /// <summary>
        /// Gets whether this Poco is ECMAStandard compliant.
        /// </summary>
        bool IsECMAStandardCompliant { get; }

        /// <summary>
        /// Gets the Poco info.
        /// </summary>
        IPocoRootInfo PocoInfo { get; }

        /// <summary>
        /// Gets the <see cref="PocoJsonPropertyInfo"/>.
        /// </summary>
        IReadOnlyList<IPocoJsonPropertyInfo> Properties { get; }
    }
}
