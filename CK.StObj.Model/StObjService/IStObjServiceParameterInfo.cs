using System;
using System.Collections.Generic;

namespace CK.Core
{
    /// <summary>
    /// Describes a <see cref="Value"/> that must be used for a
    /// Service class constructor parameter.
    /// </summary>
    public interface IStObjServiceParameterInfo
    {
        /// <summary>
        /// Gets the Type of this parameter.
        /// When <see cref="IsEnumerated"/> is true, this is the type of the enumerated object:
        /// for IReadOnlyList&lt;X&gt;, this is typeof(X).
        /// </summary>
        Type ParameterType { get; }

        /// <summary>
        /// Gets the zero-based position of the parameter in the parameter list.
        /// </summary>
        int Position { get; }

        /// <summary>
        /// Gets the name of the parameter.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets whether this parameter is a enumeration of other services.
        /// When false, the type to consider is the sigle item of <see cref="Value"/>.
        /// </summary>
        bool IsEnumerated { get; }

        /// <summary>
        /// Gets the type of the value that must be resolved and set.
        /// Null if no actual value should be built and null must be set: the parameter
        /// allows a default value and this default value must be used (no attempt to
        /// resolve this parameter should be made).
        /// When <see cref="IsEnumerated"/> is false, this list contains a single type, otherwise
        /// it contains the types that must be resolved to an array of <see cref="ParameterType"/>.
        /// </summary>
        IReadOnlyList<Type> Value { get; }
    }

}
