#region Proprietary License
/*----------------------------------------------------------------------------
* This file (Tests\CK.StObj.Engine.Tests\SimpleObjects\IAbstractionA.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using CK.Core;

namespace CK.StObj.Engine.Tests.SimpleObjects
{
    public interface IAbstractionA : IRealObject
    {
        int ConstructCount { get; }
        
        void MethofOfA();
    }
}
