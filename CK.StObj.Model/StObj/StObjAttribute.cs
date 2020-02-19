#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Model\StObj\Attribute\StObjAttribute.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System;

namespace CK.Core
{
    /// <summary>
    /// Default implementation of <see cref="Setup.IStObjAttribute"/>.
    /// </summary>
    [AttributeUsage( AttributeTargets.Class, AllowMultiple = false, Inherited = false )]
    public class StObjAttribute : Attribute, Setup.IStObjAttribute
    {
        /// <summary>
        /// Gets or sets the container of the object.
        /// This property is inherited from base classes that are not Real Objects.
        /// </summary>
        public Type Container { get; set; }

        /// <summary>
        /// Gets or sets how this object must be considered regarding other items: it can be a <see cref="DependentItemKindSpec.Item"/>, 
        /// a <see cref="DependentItemKindSpec.Group"/> or a <see cref="DependentItemKindSpec.Container"/>.
        /// When let to the default <see cref="DependentItemKindSpec.Unknown"/>, this property is inherited (it is eventually 
        /// considered as <see cref="DependentItemKindSpec.Container"/> when not set).
        /// This property is inherited from base classes that are not Real Objects.
        /// </summary>
        public DependentItemKindSpec ItemKind { get; set; }

        /// <summary>
        /// Gets or sets how Ambient Properties that reference the object must be tracked.
        /// This property is inherited from base classes that are not Real Objects.
        /// </summary>
        public TrackAmbientPropertiesMode TrackAmbientProperties { get; set; }

        /// <summary>
        /// Gets or sets an array of direct dependencies.
        /// This property is not inherited, it applies only to the decorated type.
        /// </summary>
        public Type[] Requires { get; set; }

        /// <summary>
        /// Gets or sets an array of types that depend on the object.
        /// This property is not inherited, it applies only to the decorated type.
        /// </summary>
        public Type[] RequiredBy { get; set; }

        /// <summary>
        /// Gets or sets an array of types that must be Children of this item.
        /// <see cref="ItemKind"/> must be <see cref="DependentItemKindSpec.Group"/> or <see cref="DependentItemKindSpec.Container"/>.
        /// This property is not inherited, it applies only to the decorated type.
        /// </summary>
        public Type[] Children { get; set; }

        /// <summary>
        /// Gets or sets an array of types that must be considered as groups for this item.
        /// This property is not inherited, it applies only to the decorated type.
        /// </summary>
        public Type[] Groups { get; set; }

    }
}
