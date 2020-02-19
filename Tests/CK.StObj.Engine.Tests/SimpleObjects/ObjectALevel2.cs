#region Proprietary License
/*----------------------------------------------------------------------------
* This file (Tests\CK.StObj.Engine.Tests\SimpleObjects\ObjectALevel2.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System.Reflection;
using CK.Core;
using NUnit.Framework;

namespace CK.StObj.Engine.Tests.SimpleObjects
{
    // ObjectALevel2 is by default in the container of its parent: ObjectALevel1 is in PackageForABLevel1.
    public class ObjectALevel2 : ObjectALevel1
    {
        // Adds monitor parameter otherwise parameter less StObjConstruct are not called.
        void StObjConstruct( IActivityMonitor m )
        {
            Assert.That( ConstructCount, Is.EqualTo( 2 ), "ObjectA and ObjectALevel1 construct has been called." );
            SimpleObjectsTrace.LogMethod( GetType().GetMethod( "StObjConstruct", BindingFlags.Instance | BindingFlags.NonPublic ) );
            ConstructCount = ConstructCount + 1;
        }

    }
}
