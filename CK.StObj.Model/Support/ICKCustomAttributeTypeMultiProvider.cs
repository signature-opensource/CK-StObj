#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Runtime\ICustomAttributeMultiProvider.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System;
using System.Collections.Generic;
using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// Specialized <see cref="ICKCustomAttributeMultiProvider"/> bound to a <see cref="P:Type"/>. 
    /// Attributes of the Type itself MUST be available from this interface.
    /// </summary>
    public interface ICKCustomAttributeTypeMultiProvider : ICKCustomAttributeMultiProvider
    {
        /// <summary>
        /// Gets the type info to which this provider is bound.
        /// The attributes of this type are available (recall that a Type is a MemberInfo).
        /// </summary>
        Type Type { get; }

        /// <summary>
        /// Gets all attributes that are assignable to the given <paramref name="attributeType"/>, regardless of the <see cref="MemberInfo"/>
        /// that carries it. 
        /// </summary>
        /// <param name="attributeType">Type of requested attributes.</param>
        /// <param name="memberOnly">True to ignore attributes of the type itself.</param>
        /// <returns>Enumeration of attributes (possibly empty).</returns>
        IEnumerable<object> GetAllCustomAttributes( Type attributeType, bool memberOnly = false );

        /// <summary>
        /// Gets all attributes that are assignable to the given type, regardless of the <see cref="MemberInfo"/>
        /// that carries it.
        /// </summary>
        /// <typeparam name="T">Type of the attributes.</typeparam>
        /// <param name="memberOnly">True to ignore attributes of the type itself.</param>
        /// <returns>Enumeration of attributes (possibly empty).</returns>
        IEnumerable<T> GetAllCustomAttributes<T>( bool memberOnly = false);

    }
}
