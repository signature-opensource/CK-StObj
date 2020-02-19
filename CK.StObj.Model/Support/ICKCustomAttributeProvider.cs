#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Runtime\ICustomAttributeProvider.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System;
using System.Collections.Generic;
using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// Offers a way to retrieve attributes on <see cref="MemberInfo"/>.
    /// This is a basic interface that is not bound to a type or an assembly or to any special holders.
    /// Its goal is to support a way to inject behavior related to attributes such as caching them for instance in 
    /// order to enable attributes to share state between different aspects (but inside the same process or context).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This interface does not say anything about the members that are requested. 
    /// Specialization or implementation may restrict the accepted members: <see cref="ICKCustomAttributeMultiProvider"/> for instance
    /// is bound to a <see cref="Type"/> (FYI: a Type is a <see cref="MemberInfo"/>).
    /// </para>
    /// <para>
    /// It is named ICKCustomAttributeProvider because System.Reflection.ICustomAttributeProvider is already defined by .Net but 
    /// is much more restrictive since it accepts Type objects only. This interface extends the concept to support any MemberInfo 
    /// objects.
    /// </para>
    /// </remarks>
    public interface ICKCustomAttributeProvider
    {
        /// <summary>
        /// Gets whether an attribute that is assignable to the given <paramref name="attributeType"/> 
        /// exists on the given member.
        /// </summary>
        /// <param name="m">The member info (can be a <see cref="Type"/>).</param>
        /// <param name="attributeType">Type of requested attributes.</param>
        /// <returns>True if at least one attribute exists.</returns>
        bool IsDefined( MemberInfo m, Type attributeType );

        /// <summary>
        /// Gets all the attributes that are assignable to the given <paramref name="attributeType"/>.
        /// </summary>
        /// <param name="m">The member info (can be a <see cref="Type"/>).</param>
        /// <param name="attributeType">Type of requested attributes.</param>
        /// <returns>A set of attributes that are guaranteed to be assignable to <paramref name="attributeType"/>. Can be null or empty.</returns>
        IEnumerable<object> GetCustomAttributes( MemberInfo m, Type attributeType );

        /// <summary>
        /// Gets all the attributes that are assignable to the given <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Type of the attributes.</typeparam>
        /// <param name="m">The member info (can be a <see cref="Type"/>).</param>
        /// <returns>A set of typed attributes. Can be null or empty.</returns>
        IEnumerable<T> GetCustomAttributes<T>( MemberInfo m );
    }
}
