#region Proprietary License
/*----------------------------------------------------------------------------
* This file (Tests\CK.StObj.Engine.Tests\SimpleObjects\ObjectALevel3.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System.Reflection;
using CK.Core;
using NUnit.Framework;

namespace CK.StObj.Engine.Tests.SimpleObjects
{
    // ObjectALevel2 is by default in the container of its parent's parent: ObjectALevel1 is in PackageForABLevel1
    public class ObjectALevel3 : ObjectALevel2, IAbstractionALevel3
    {
        // Adds monitor parameter otherwise parameter less StObjConstruct are not called.
        void StObjConstruct( IActivityMonitor m  ) 
        {
            Assert.That( ConstructCount, Is.EqualTo( 3 ), "ObjectA, ObjectALevel1 and ObjectALevel2 construct have been called." );
            SimpleObjectsTrace.LogMethod( GetType().GetMethod( "StObjConstruct", BindingFlags.Instance | BindingFlags.NonPublic ) );
            ConstructCount = ConstructCount + 1;
        }

        public virtual void MethofOfALevel3()
        {
            SimpleObjectsTrace.LogMethod( GetType().GetMethod( "MethofOfALevel3", BindingFlags.Instance | BindingFlags.Public ) );
        }

    }
}
