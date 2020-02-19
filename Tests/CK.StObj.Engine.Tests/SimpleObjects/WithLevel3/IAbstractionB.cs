#region Proprietary License
/*----------------------------------------------------------------------------
* This file (Tests\CK.StObj.Engine.Tests\SimpleObjects\WithLevel3\IAbstractionB.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using CK.Core;

namespace CK.StObj.Engine.Tests.SimpleObjects.WithLevel3
{
    public interface IAbstractionBOnLevel2 : IRealObject
    {
        int ConstructCount { get; }
        
        void MethofOfBOnLevel2();
    }
}
