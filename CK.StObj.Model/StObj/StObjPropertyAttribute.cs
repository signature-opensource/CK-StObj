#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Model\StObj\Attribute\StObjPropertyAttribute.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System;

namespace CK.Core
{
    /// <summary>
    /// Defines a StObj property. Can be set on a property or on a class.
    /// When defined on a class, there can be no real .Net property with the <see cref="PropertyName"/> on the
    /// object.
    /// </summary>
    [AttributeUsage( AttributeTargets.Property|AttributeTargets.Class, AllowMultiple=true, Inherited=true )]
    public class StObjPropertyAttribute : Attribute
    {

        /// <summary>
        /// Initialize a new attribute on an existing property <see cref="PropertyName"/> and <see cref="PropertyType"/>
        /// are the one of the property.
        /// </summary>
        public StObjPropertyAttribute()
        {
            ResolutionSource = PropertyResolutionSource.FromContainerAndThenGeneralization;
        }
        
        /// <summary>
        /// Intitalize a new attribute on a class: <see cref="PropertyName"/> and <see cref="PropertyType"/> must 
        /// be defined.
        /// </summary>
        /// <param name="propertyName">Name of the StObj property.</param>
        /// <param name="propertyType">Type of the StObj property.</param>
        public StObjPropertyAttribute( string propertyName, Type propertyType )
        {
            PropertyName = propertyName;
            PropertyType = propertyType;
            ResolutionSource = PropertyResolutionSource.FromContainerAndThenGeneralization;
        }

        /// <summary>
        /// Gets or sets the <see cref="PropertyResolutionSource"/> for this property.
        /// Defaults to <see cref="PropertyResolutionSource.FromContainerAndThenGeneralization"/>.
        /// </summary>
        public PropertyResolutionSource ResolutionSource { get; set; }

        /// <summary>
        /// Gets or sets the name of the StObj property. When the attribute is set on an actual property, this 
        /// name, when set, takes precedence over the property name.
        /// </summary>
        public string PropertyName { get; set; }
        
        /// <summary>
        /// Gets or sets the type of the StObj property. When the attribute is set on an actual 
        /// property and this type is set, it takes precedence over the actual property type.
        /// </summary>
        public Type PropertyType { get; set; }
    }
}
