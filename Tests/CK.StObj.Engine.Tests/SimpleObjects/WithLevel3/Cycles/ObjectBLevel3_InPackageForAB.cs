#region Proprietary License
/*----------------------------------------------------------------------------
* This file (Tests\CK.StObj.Engine.Tests\SimpleObjects\WithLevel3\Cycles\ObjectBLevel3_InPackageForAB.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using CK.Core;
using NUnit.Framework;


namespace CK.StObj.Engine.Tests.SimpleObjects.WithLevel3.Cycles;

public class ObjectBLevel3_InPackageForAB : ObjectBLevel2
{
    void StObjConstruct( [Container] PackageForAB package )
    {
        Assert.Fail( "Since this creates a Cycle, the object graph is not created." );
    }

}
