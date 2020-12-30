#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Runtime\IStObjMutableParameter.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using CK.Core;
using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// Describes a parameter of a StObjConstruct method.
    /// </summary>
    public interface IStObjMutableParameter : IStObjMutableReference
    {
        /// <summary>
        /// Gets the name of the construct parameter.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets or sets whether the resolution of this parameter can be considered as optional.
        /// When changed by <see cref="IStObjStructuralConfigurator"/> from false to true (see remarks) and the resolution fails, the default 
        /// value of the parameter or the default value for the parameter's type is automatically used (null for reference types).
        /// </summary>
        /// <remarks>
        /// If this is originally false, it means that the formal parameter of the method is NOT optional (<see cref="IsRealParameterOptional"/> is false). 
        /// </remarks>
        bool IsOptional { get; set; }

        /// <summary>
        /// Gets the parameter position in the list.
        /// </summary>
        int Index { get; }

        /// <summary>
        /// Gets whether the formal parameter is optional (<see cref="System.Type.Missing"/> can be used as the parameter value 
        /// at invocation time, see <see cref="ParameterInfo.IsOptional"/>).
        /// </summary>
        bool IsRealParameterOptional { get; }

        /// <summary>
        /// Sets the value for this parameter.
        /// By setting an explicit value through this method, the <see cref="IStObjMutableReference.Type"/> that describes
        /// a reference to a <see cref="IStObjResult"/> are ignored: this breaks the potential dependency to
        /// the <see cref="IRealObject"/> object that may be referenced.
        /// </summary>
        /// <remarks>
        /// The <see cref="IStObjFinalParameter"/> also exposes this method: by using <see cref="IStObjFinalParameter.SetParameterValue"/>
        /// from <see cref="IStObjValueResolver.ResolveParameterValue"/>, an explicit value can be injected while the potential
        /// dependency has actually been taken into account.
        /// </remarks>
        /// <param name="value">
        /// Value to set. Type must be compatible otherwise an exception will be thrown when calling the
        /// actual StObjConstruct method.
        /// </param>
        void SetParameterValue( object value );
    }
}
