#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Engine\StObj\Impl\MutableReferenceWithValue.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion


namespace CK.Setup
{
    /// <summary>
    /// Base class for construct parameters or ambient properties: these references can be resolved
    /// either structurally or dynamically (by <see cref="IStObjValueResolver"/>).
    /// </summary>
    internal abstract class MutableReferenceWithValue : MutableReferenceOptional
    {
        internal MutableReferenceWithValue( MutableItem owner, StObjMutableReferenceKind kind )
            : base( owner, kind )
        {
            Value = System.Type.Missing;
        }

        public object Value { get; protected set; }
    }
}
