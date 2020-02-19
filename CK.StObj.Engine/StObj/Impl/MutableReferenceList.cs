#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Engine\StObj\Impl\MutableReferenceList.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System;
using System.Collections.Generic;

namespace CK.Setup
{
    internal class MutableReferenceList : List<MutableReference>, IStObjMutableReferenceList
    {
        MutableItem _owner;
        StObjMutableReferenceKind _kind;

        internal MutableReferenceList( MutableItem owner, StObjMutableReferenceKind kind )
        {
            _owner = owner;
            _kind = kind;
        }

        // To disambiguate types.
        internal List<MutableReference> AsList => this;

        public IStObjMutableReference AddNew( Type t, StObjRequirementBehavior behavior )
        {
            var m = new MutableReference( _owner, _kind ) { Type = t, StObjRequirementBehavior = behavior };
            Add( m );
            return m;
        }

        public int IndexOf( object item )
        {
            MutableReference m = item as MutableReference;
            return m != null ? IndexOf( m ) : Int32.MinValue;
        }

        IStObjMutableReference IReadOnlyList<IStObjMutableReference>.this[int index] => this[index]; 

        IEnumerator<IStObjMutableReference> IEnumerable<IStObjMutableReference>.GetEnumerator() => GetEnumerator();

    }

}
