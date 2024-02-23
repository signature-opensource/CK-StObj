using CK.Core;
using System.Collections.Generic;

namespace CK.Setup.Json
{
    /// <summary>
    /// Captures Json information for a <see cref="IPocoFamilyInfo"/>.
    /// If available (if the CK.Poco.Json package is installed and the generation succeeds)
    /// this can be obtained on the <see cref="IPocoFamilyInfo"/> by using the <see cref="IAnnotationSet.Annotation{T}"/> API
    /// or the more convenient <see cref="PocoPropertyExtensions.GetJsonInfo"/> extension method.
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
        IPocoFamilyInfo PocoInfo { get; }

        /// <summary>
        /// Gets the <see cref="IPocoJsonPropertyInfo"/> for each properties (in the same order).
        /// </summary>
        IReadOnlyList<IPocoJsonPropertyInfo> JsonProperties { get; }
    }
}
