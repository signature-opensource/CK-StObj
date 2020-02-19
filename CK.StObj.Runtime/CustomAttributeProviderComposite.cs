#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Runtime\CustomAttributeProviderComposite.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// Helper class that composes multiple <see cref="ICKCustomAttributeProvider"/> into one.
    /// </summary>
    public class CustomAttributeProviderComposite : ICKCustomAttributeProvider
    {
        readonly IEnumerable<ICKCustomAttributeProvider> _providers;

        /// <summary>
        /// Initializes a new <see cref="CustomAttributeProviderComposite"/> bound to multiple <see cref="ICKCustomAttributeProvider"/>.
        /// </summary>
        /// <param name="providers">Multiple providers. Must not be null.</param>
        public CustomAttributeProviderComposite( IEnumerable<ICKCustomAttributeProvider> providers )
        {
            if( providers == null ) throw new ArgumentNullException( "providers" ); 
            _providers = providers;
        }

        /// <summary>
        /// The attribute is defined if at least one of the multiple providers defines it.
        /// </summary>
        /// <param name="m">The member info (can be a <see cref="Type"/>).</param>
        /// <param name="attributeType">Type of requested attributes.</param>
        /// <returns>True if at least one attribute exists.</returns>
        public bool IsDefined( MemberInfo m, Type attributeType )
        {
            return _providers.Any( p => p.IsDefined( m, attributeType ) );
        }

        /// <summary>
        /// Gets the combined list of all attributes from the multiple providers.
        /// </summary>
        /// <param name="m">The member info (can be a <see cref="Type"/>).</param>
        /// <param name="attributeType">Type of requested attributes.</param>
        /// <returns>A set of attributes that are guaranteed to be assignable to <paramref name="attributeType"/>. Can be null or empty.</returns>
        public IEnumerable<object> GetCustomAttributes( MemberInfo m, Type attributeType )
        {
            return _providers.SelectMany( p => p.GetCustomAttributes( m, attributeType ) );
        }


        /// <summary>
        /// Gets the combined list of all of attributes that are assignable to the given <typeparamref name="T"/> from the multiple providers..
        /// </summary>
        /// <typeparam name="T">Type of the attributes.</typeparam>
        /// <param name="m">The member info (can be a <see cref="Type"/>).</param>
        /// <returns>A set of typed attributes. Can be null or empty.</returns>
        public IEnumerable<T> GetCustomAttributes<T>( MemberInfo m )
        {
            return _providers.SelectMany( p => p.GetCustomAttributes<T>( m ) );
        }
    }


}
