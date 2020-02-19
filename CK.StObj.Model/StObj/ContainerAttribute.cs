#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Model\StObj\Attribute\ContainerAttribute.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System;

namespace CK.Core
{
    /// <summary>
    /// Parameter attribute that can be use to designate the container of the object among 
    /// StObjConstruct method parameters.
    /// </summary>
    [AttributeUsage( AttributeTargets.Parameter, Inherited=false, AllowMultiple=false )]
    public class ContainerAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new <see cref="ContainerAttribute"/>.
        /// </summary>
        public ContainerAttribute()
        {
        }
    }
}
