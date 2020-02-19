#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Model\StObj\Attribute\IStObjAttribute.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using CK.Core;
using System;

namespace CK.Setup
{
    /// <summary>
    /// Basic support for declarative dependency structure between types.
    /// </summary>
    public interface IStObjAttribute
    {
        /// <summary>
        /// Gets the container of the object.
        /// This property is inherited from base classes that are not Real Objects.
        /// </summary>
        Type Container { get; }

        /// <summary>
        /// Gets the kind of object (simple item, group or container).
        /// This property is inherited from base classes that are not Real Objects.
        /// </summary>
        DependentItemKindSpec ItemKind { get; }

        /// <summary>
        /// Gets how Ambient Properties that reference the object must be tracked.
        /// This property is inherited from base classes that are not Real Objects.
        /// </summary>
        TrackAmbientPropertiesMode TrackAmbientProperties { get; }

        /// <summary>
        /// Gets an array of direct dependencies.
        /// This property is not inherited, it applies only to the decorated type.
        /// </summary>
        Type[] Requires { get; }

        /// <summary>
        /// Gets an array of types that depends on the object.
        /// This property is not inherited, it applies only to the decorated type.
        /// </summary>
        Type[] RequiredBy { get; }

        /// <summary>
        /// Gets an array of types that must be Children of this item.
        /// <see cref="ItemKind"/> must be <see cref="DependentItemKindSpec.Group"/> or <see cref="DependentItemKindSpec.Container"/>.
        /// This property is not inherited, it applies only to the decorated type.
        /// </summary>
        Type[] Children { get; }

        /// <summary>
        /// Gets an array of types that must be considered as groups for this item.
        /// This property is not inherited, it applies only to the decorated type.
        /// </summary>
        Type[] Groups { get; }
    }
}
