#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Model\AutoImplementor\IAttributeAutoImplemented.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion


namespace CK.Setup
{

    /// <summary>
    /// Interface marker for attributes that mark property or method that can be automatically implemented.
    /// This interface states that there is a way to implement it, but does not provide it.
    /// </summary>
    /// <remarks>
    /// See <see cref="IAutoImplementorMethod"/> and <see cref="IAutoImplementorProperty"/>
    /// that are able to actually implement methods and properties.
    /// Attributes that support those interfaces can directly provide an implementation: when an attribute only support
    /// this <see cref="IAttributeAutoImplemented"/> marker, the implementation is a stub provided by CK.Reflection.EmitHelper.ImplementEmptyStubMethod 
    /// or CK.Reflection.EmitHelper.ImplementEmptyStubProperty helpers.
    /// </remarks>
    public interface IAttributeAutoImplemented
    {
    }
}
