#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Runtime\IStObjFinalParameter.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion


namespace CK.Setup
{
    /// <summary>
    /// Exposes the parameter of a StObjConstruct method that <see cref="IStObjValueResolver.ResolveParameterValue"/> sees.
    /// </summary>
    public interface IStObjFinalParameter : IStObjReference
    {
        /// <summary>
        /// Gets the StObj that owns this reference as a <see cref="IStObjResult"/> (since the dependency graph is resolved).
        /// This owner corresponds to the exact type of the object that has the StObjConstruct method for parameters.
        /// </summary>
        new IStObjResult Owner { get; }
        
        /// <summary>
        /// Gets the name of the construct parameter.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the zero based parameter position in the list.
        /// </summary>
        int Index { get; }

        /// <summary>
        /// Gets whether this reference can be considered as optional. When true, <see cref="Value"/> can be <see cref="System.Type.Missing"/>:
        /// if automatic resolution fails then, for a property it is simply not set and, for a parameter, behavior depends on <see cref="IStObjFinalParameter.IsRealParameterOptional"/>.
        /// </summary>
        bool IsOptional { get; }

        /// <summary>
        /// Gets whether the formal parameter is actually optional. 
        /// When both this and <see cref="IsOptional"/> are true and <see cref="Value"/> has not been resolved, <see cref="System.Type.Missing"/> will be 
        /// used as the parameter value at invocation time. When this is false, the default value for the expected type is used.
        /// </summary>
        bool IsRealParameterOptional { get; }

        /// <summary>
        /// Gets the current value that will be used. 
        /// If it has not been resolved to a <see cref="IStObjResult.InitialObject"/> instance or "structurally" set by one <see cref="IStObjStructuralConfigurator"/>, it is <see cref="System.Type.Missing"/>. 
        /// Use <see cref="SetParameterValue"/> to set it.
        /// </summary>
        object Value { get; }

        /// <summary>
        /// Sets the <see cref="Value"/> for this parameter.
        /// </summary>
        /// <remarks>
        /// The <see cref="IStObjMutableParameter"/> also exposes this method: by using <see cref="IStObjMutableParameter.SetParameterValue"/>
        /// method from <see cref="IStObjStructuralConfigurator.Configure"/>, the explicit value is injected and breaks (suppress)
        /// the potential dependency to this <see cref="IStObjReference.Type"/>.
        /// </remarks>
        /// <param name="value">
        /// Value to set. Type must be compatible otherwise an exception will be thrown when
        /// calling the actual StObjConstruct method.
        /// </param>
        void SetParameterValue( object value );

    }
}
