#region Proprietary License
/*----------------------------------------------------------------------------
* This file (Tests\CK.StObj.Engine.Tests\SimpleObjects\ObjectBLevel1.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System.Reflection;
using NUnit.Framework;
using CK.Core;

namespace CK.StObj.Engine.Tests.SimpleObjects
{
    [StObj( Container = typeof( PackageForABLevel1 ) )]
    public class ObjectBLevel1 : ObjectB
    {
        // Adds monitor parameter otherwise parameter less StObjConstruct are not called.
        void StObjConstruct( IActivityMonitor m )
        {
            Assert.That( ConstructCount, Is.EqualTo( 1 ), "ObjectB.StObjConstruct has been called.");
            SimpleObjectsTrace.LogMethod( GetType().GetMethod( "StObjConstruct", BindingFlags.Instance | BindingFlags.NonPublic ) );
            ConstructCount = ConstructCount + 1;
        }
    }
}
