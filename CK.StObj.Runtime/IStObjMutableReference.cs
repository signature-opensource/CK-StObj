#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Runtime\IStObjMutableReference.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System;

namespace CK.Setup
{
    /// <summary>
    /// Mutable version of <see cref="IStObjReference"/>: <see cref="Type"/>
    /// and <see cref="StObjRequirementBehavior"/> properties are settable.
    /// </summary>
    public interface IStObjMutableReference : IStObjReference
    {
        /// <summary>
        /// Gets the item that owns this reference.
        /// </summary>
        new IStObjMutableItem Owner { get; }

        /// <summary>
        /// Gets or sets the type of the reference. Can be set to null: container and requirements are ignored and 
        /// construct parameters are resolved to their default (<see cref="IStObjMutableParameter.IsOptional"/> must be true).
        /// Of course, for construct parameters the type must be compatible with the formal parameter's type (similar
        /// type compatibility is required for ambient properties).
        /// </summary>
        /// <remarks>
        /// Initialized with the <see cref="System.Reflection.PropertyInfo.PropertyType"/> for Ambient Properties or Singletons, 
        /// with <see cref="System.Reflection.ParameterInfo.ParameterType"/> for parameters and with provided type 
        /// for other kind of reference (<see cref="StObjMutableReferenceKind.Requires"/>, <see cref="StObjMutableReferenceKind.RequiredBy"/>, <see cref="StObjMutableReferenceKind.Group"/>, 
        /// <see cref="StObjMutableReferenceKind.Child"/> and <see cref="StObjMutableReferenceKind.Container"/>).
        /// </remarks>
        new Type Type { get; set; }

        /// <summary>
        /// Gets or sets whether this reference must be satisfied with an available <see cref="IStObjResult"/> if the <see cref="P:Type"/> is not set to null.
        /// <para>
        /// Defaults to <see cref="StObjRequirementBehavior.ErrorIfNotStObj"/> for <see cref="IStObjMutableItem.SpecializedInjectObjects">Inject Objects</see>, <see cref="IStObjMutableItem.Requires"/> 
        /// and <see cref="IStObjMutableItem.Container"/> (a described dependency is required unless explicitly declared as optional by <see cref="IStObjStructuralConfigurator"/>).
        /// </para>
        /// <para>
        /// Defaults to <see cref="StObjRequirementBehavior.WarnIfNotStObj"/> for StObjConstruct parameters since <see cref="IStObjValueResolver"/> can inject any dependency (the 
        /// dependency may even be missing - ie. let to null for reference types and to the default value for value type - if <see cref="IStObjMutableParameter.IsOptional"/> is true).
        /// </para>
        /// <para>
        /// Defaults to <see cref="StObjRequirementBehavior.None"/> for ambient properties and <see cref="IStObjMutableItem.RequiredBy"/> since "required by" are always considered as optional
        /// and ambient properties are not necessarily bound to another Structured Object.
        /// </para>
        /// </summary>
        new StObjRequirementBehavior StObjRequirementBehavior { get; set; }

    }
}
