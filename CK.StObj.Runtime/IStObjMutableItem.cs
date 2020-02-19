#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Runtime\IStObjMutableItem.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System;
using System.Collections.Generic;
using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Mutable object. This support (re)configuration of the objects.
    /// </summary>
    public interface IStObjMutableItem
    {
        /// <summary>
        /// Gets the associated object instance (the final, most specialized, structured object).
        /// The final object is exposed by this interface to allow (exceptional) direct accesses to "intrinsic" properties (fields or methods) 
        /// of the object (like readonly properties initalized by constructor) but care should be taken when accessing
        /// the final object since, depending of the current step in the process, it has not necessarily been constructed/initialized correctly yet.
        /// </summary>
        object InitialObject { get; }

        /// <summary>
        /// Gets the type of the structure object.
        /// </summary>
        Type ObjectType { get; }

        /// <summary>
        /// Gets the provider for attributes. Attributes that are marked with <see cref="IAttributeContextBound"/> are cached
        /// and can keep an internal state if needed.
        /// </summary>
        /// <remarks>
        /// All attributes related to <see cref="ObjectType"/> (either on the type itself or on any of its members) should be retrieved 
        /// thanks to this method otherwise stateful attributes will not work correctly.
        /// </remarks>
        ICKCustomAttributeTypeMultiProvider Attributes { get; }

        /// <summary>
        /// Gets the kind of object (simple item, group or container).
        /// </summary>
        DependentItemKindSpec ItemKind { get; set; }

        /// <summary>
        /// Gets or sets how Ambient Properties that reference this StObj must be considered.
        /// </summary>
        TrackAmbientPropertiesMode TrackAmbientProperties { get; set; }

        /// <summary>
        /// Gets a mutable reference to the container of the object.
        /// Initialized by <see cref="StObjAttribute.Container"/> or any other <see cref="IStObjStructuralConfigurator"/>.
        /// When the configured container's type is null and this StObj has a Generalization, the container of its Generalization will be used.
        /// </summary>
        IStObjMutableReference Container { get; }

        /// <summary>
        /// Contained items of the object.
        /// Initialized by <see cref="StObjAttribute.Children"/>.
        /// </summary>
        IStObjMutableReferenceList Children { get; }

        /// <summary>
        /// Direct dependencies of the object.
        /// Initialized by <see cref="StObjAttribute.Requires"/>.
        /// </summary>
        IStObjMutableReferenceList Requires { get; }

        /// <summary>
        /// Reverse dependencies: types that depend on the object.
        /// Initialized by <see cref="StObjAttribute.RequiredBy"/>.
        /// </summary>
        IStObjMutableReferenceList RequiredBy { get; }

        /// <summary>
        /// Groups for this object.
        /// Initialized by <see cref="StObjAttribute.Groups"/>.
        /// </summary>
        IStObjMutableReferenceList Groups { get; }

        /// <summary>
        /// Gets a list of mutable StObjConstruct parameters.
        /// </summary>
        IReadOnlyList<IStObjMutableParameter> ConstructParameters { get; }

        /// <summary>
        /// Gets a list of Ambient properties defined at this level (and above) but potentially specialized.
        /// This guarantees that properties are accessed by their most precise overridden/masked version.
        /// To explicitly set a value for an ambient property or alter its configuration, use <see cref="SetAmbientPropertyValue"/>
        /// or <see cref="SetAmbientPropertyConfiguration"/>.
        /// </summary>
        IReadOnlyList<IStObjAmbientProperty> SpecializedAmbientProperties { get; }

        /// <summary>
        /// Gets a list of mutable <see cref="IStObjMutableInjectObject"/> defined at this level (and above) but potentially specialized.
        /// This guarantees that properties are accessed by their most precise overridden/masked version.
        /// </summary>
        IReadOnlyList<IStObjMutableInjectObject> SpecializedInjectObjects { get; }

        /// <summary>
        /// Sets a direct property (it must not be an Ambient Property, Singleton nor a StObj property) on the Structured Object. 
        /// The property must exist, be writable and the type of the <paramref name="value"/> must be compatible with the property type 
        /// otherwise an error is logged.
        /// </summary>
        /// <param name="monitor">The monitor to use to describe any error.</param>
        /// <param name="propertyName">Name of the property to set.</param>
        /// <param name="value">Value to set.</param>
        /// <param name="sourceDescription">Optional description of the origin of the value to help troubleshooting.</param>
        /// <returns>True on success, false if any error occurs.</returns>
        bool SetDirectPropertyValue( IActivityMonitor monitor, string propertyName, object value, string sourceDescription = null );

        /// <summary>
        /// Sets a property on the StObj. The property must not be an ambient property, but it is not required to be 
        /// defined by a <see cref="StObjPropertyAttribute"/> (see remarks).
        /// </summary>
        /// <remarks>
        /// A StObj property can be dynamically defined on any StObj. The StObjPropertyAttribute enables definition and Type restriction 
        /// of StObj properties by the holding type itself, but is not required.
        /// </remarks>
        /// <param name="monitor">The monitor to use to describe any error.</param>
        /// <param name="propertyName">Name of the property.</param>
        /// <param name="value">Value to set.</param>
        /// <param name="sourceDescription">Optional description of the origin of the value to help troubleshooting.</param>
        /// <returns>True on success, false if any error occurs.</returns>
        bool SetStObjPropertyValue( IActivityMonitor monitor, string propertyName, object value, string sourceDescription = null );

        /// <summary>
        /// Sets an ambient property on the Structured Object (the property must exist, be writable, and marked with <see cref="AmbientPropertyAttribute"/>). The
        /// type of the <paramref name="value"/> must be compatible with the property type otherwise an error is logged.
        /// </summary>
        /// <param name="monitor">The monitor to use to describe any error.</param>
        /// <param name="propertyName">Name of the property to set.</param>
        /// <param name="value">Value to set.</param>
        /// <param name="sourceDescription">Optional description of the origin of the value to help troubleshooting.</param>
        /// <returns>True on success, false if any error occurs.</returns>
        bool SetAmbientPropertyValue( IActivityMonitor monitor, string propertyName, object value, string sourceDescription = null );

        /// <summary>
        /// Sets how an ambient property on the Structured Object must be resolved (the property must exist, 
        /// be writeable, and marked with <see cref="AmbientPropertyAttribute"/>).
        /// </summary>
        /// <param name="monitor">The monitor to use to describe any error.</param>
        /// <param name="propertyName">Name of the property to configure.</param>
        /// <param name="type">See <see cref="IStObjMutableReference.Type"/>.</param>
        /// <param name="behavior">See <see cref="IStObjMutableReference.StObjRequirementBehavior"/>.</param>
        /// <param name="sourceDescription">Optional description of the origin of the call to help troubleshooting.</param>
        /// <returns>True on success, false if any error occurs.</returns>
        bool SetAmbientPropertyConfiguration( IActivityMonitor monitor, string propertyName, Type type, StObjRequirementBehavior behavior, string sourceDescription = null );

    }
}
