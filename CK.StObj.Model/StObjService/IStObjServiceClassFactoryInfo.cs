using System.Collections.Generic;

namespace CK.Core
{
    /// <summary>
    /// Describes the parameters assignments that are required to call
    /// the constructor of a Service class Type.
    /// </summary>
    public interface IStObjServiceClassFactoryInfo : IStObjServiceClassDescriptor
    {
        /// <summary>
        /// Gets the set of parameters assignments of the single <see cref="IStObjFinalClass.ClassType">ClassType</see>'s
        /// public constructor that must be explicitly provided in order to successfully
        /// call the constructor.
        /// <para>
        /// Only parameters that require a <see cref="IStObjServiceParameterInfo"/> appear in this list.
        /// </para>
        /// </summary>
        IReadOnlyList<IStObjServiceParameterInfo> Assignments { get; }
    }

}
