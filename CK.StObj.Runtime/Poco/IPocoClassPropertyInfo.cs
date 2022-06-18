using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace CK.Setup
{
    /// <summary>
    /// Describes a Poco-like property.
    /// </summary>
    public interface IPocoClassPropertyInfo : IPocoBasePropertyInfo
    {
        /// <summary>
        /// Gets the property info.
        /// </summary>
        PropertyInfo PropertyInfo { get; }
    }
}
