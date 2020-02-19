#region Proprietary License
/*----------------------------------------------------------------------------
* This file (Tests\CK.StObj.Engine.Tests\SimpleObjects\ObjectALevel1.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System.Reflection;
using CK.Core;
using NUnit.Framework;

namespace CK.StObj.Engine.Tests.SimpleObjects
{
    public class ObjectALevel1 : ObjectA
    {
        ObjectBLevel1 _oB;

        void StObjConstruct( [Container]PackageForABLevel1 package, ObjectBLevel1 oB )
        {
            Assert.That( ConstructCount, Is.EqualTo( 1 ), "ObjectA.StObjConstruct has been called.");
            Assert.That( oB.ConstructCount, Is.GreaterThanOrEqualTo( 2 ), "ObjectB and ObjectBLevel1 StObjConstruct have been called.");
            Assert.That( package.ConstructCount, Is.GreaterThanOrEqualTo( 2 ), "PackageForAB and PackageForABLevel1 StObjConstruct have been called.");

            SimpleObjectsTrace.LogMethod( GetType().GetMethod( "StObjConstruct", BindingFlags.Instance | BindingFlags.NonPublic ) );
            _oB = oB;

            ConstructCount = ConstructCount + 1;
        }

    }
}
