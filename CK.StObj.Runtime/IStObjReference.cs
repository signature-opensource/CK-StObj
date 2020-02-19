#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Runtime\IStObjReference.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System;
using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Describes a certain <see cref="Kind"/> of reference originating from a <see cref="Owner"/>,
    /// targeting a <see cref="Type"/> that can have some <see cref="StObjRequirementBehavior">requirements</see>.
    /// This interface describes an immutable object.
    /// Specialized interfaces like <see cref="IStObjMutableReference"/> mask its properties with setter and/or gives access
    /// to other mutable objects (like <see cref="IStObjMutableReference.Owner"/> that is a <see cref="IStObjMutableItem"/>).
    /// </summary>
    public interface IStObjReference
    {
        /// <summary>
        /// Gets the StObj that owns this reference.
        /// Sepecialized interfaces maks this with a more precise type depending of the current step of the build process.
        /// See remarks.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Owner of the reference corresponds to the exact type of the object that has the StObjConstruct method for parameters.
        /// However, for Ambient Properties, the Owner is the ultimate (leaf) Specialization.
        /// </para>
        /// <para>
        /// For Ambient Properties, this is because a property has de facto more than one Owner when masking is used: spotting one of them requires to 
        /// choose among them - The more abstract one? The less abstract one? - and this would be both ambiguate and quite useless since in practice, 
        /// the "best Owner" must be based on the actual property type to take Property Covariance into account.
        /// </para>
        /// </remarks>
        IStObj Owner { get; }

        /// <summary>
        /// Gets the kind of reference (Container, Requires, RequiredBy, Group, Child, ConstructParameter,
        /// AmbientProperty or InjectObject).
        /// </summary>
        StObjMutableReferenceKind Kind { get; }

        /// <summary>
        /// Gets the type of the reference. Can be null: container and requirements are ignored and 
        /// construct parameters are resolved to their default (<see cref="IStObjMutableParameter.IsOptional"/> must be true).
        /// Of course, for construct parameters the type must be compatible with the formal parameter's type (similar
        /// type compatibility is required for ambient properties or real objects).
        /// </summary>
        /// <remarks>
        /// Initialized with the <see cref="System.Reflection.PropertyInfo.PropertyType"/> for Ambient Properties or Ambient Contracts, 
        /// with <see cref="System.Reflection.ParameterInfo.ParameterType"/> for parameters and with provided type 
        /// for other kind of reference (<see cref="StObjMutableReferenceKind.Requires"/>, <see cref="StObjMutableReferenceKind.RequiredBy"/>, <see cref="StObjMutableReferenceKind.Group"/>, 
        /// <see cref="StObjMutableReferenceKind.Child"/> and <see cref="StObjMutableReferenceKind.Container"/>).
        /// </remarks>
        Type Type { get; }

        /// <summary>
        /// Gets whether this reference must be satisfied with an available <see cref="IStObjResult"/> if the <see cref="P:Type"/> is not null.
        /// <para>
        /// Defaults to <see cref="StObjRequirementBehavior.ErrorIfNotStObj"/> for <see cref="IStObjMutableItem.Requires"/> and <see cref="IStObjMutableItem.Container"/> 
        /// (a described dependency is required unless explicitly declared as optional by <see cref="IStObjStructuralConfigurator"/>).
        /// </para>
        /// <para>
        /// Defaults to <see cref="StObjRequirementBehavior.WarnIfNotStObj"/> for StObjConstruct parameters since <see cref="IStObjValueResolver"/> can inject any dependency (the 
        /// dependency may even be missing - ie. let to null for reference types - if <see cref="IStObjMutableParameter.IsOptional"/> is true).
        /// </para>
        /// <para>
        /// Defaults to <see cref="StObjRequirementBehavior.None"/> for ambient properties and <see cref="IStObjMutableItem.RequiredBy"/> since "required by" are always considered as optional
        /// and ambient properties are not necessarily bound to another Structured Object.
        /// </para>
        /// </summary>
        StObjRequirementBehavior StObjRequirementBehavior { get; }

    }
}
