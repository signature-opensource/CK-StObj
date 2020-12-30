#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Runtime\IStObjMutableReferenceList.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System;
using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Provides mutable list of <see cref="IStObjMutableReference"/>: items can be changed and the list itself
    /// can be modified either by <see cref="AddNew">adding new references</see> or <see cref="RemoveAt">removing</see>
    /// existing ones.
    /// </summary>
    public interface IStObjMutableReferenceList : IReadOnlyList<IStObjMutableReference>
    {
        /// <summary>
        /// Adds a new <see cref="IStObjMutableReference"/> to the list.
        /// </summary>
        /// <param name="type">Type of the reference.</param>
        /// <param name="behavior">Requirement for the referenced type.</param>
        /// <returns>The newly added <see cref="IStObjMutableReference"/>.</returns>
        IStObjMutableReference AddNew( Type type, StObjRequirementBehavior behavior );

        /// <summary>
        /// Removes a reference at a given index from this list. 
        /// </summary>
        /// <param name="index">The index of the item to remove.</param>
        void RemoveAt( int index );
    }

}
