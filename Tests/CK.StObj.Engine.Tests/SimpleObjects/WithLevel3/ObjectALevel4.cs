#region Proprietary License
/*----------------------------------------------------------------------------
* This file (Tests\CK.StObj.Engine.Tests\SimpleObjects\WithLevel3\ObjectALevel4.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System.Reflection;
using NUnit.Framework;

namespace CK.StObj.Engine.Tests.SimpleObjects.WithLevel3
{
    public class ObjectALevel4 : ObjectALevel3
    {
        void StObjConstruct( IAbstractionBOnLevel2 oB )
        {
            Assert.That( ConstructCount, Is.EqualTo( 4 ), "ObjectA, ObjectALevel1ObjectALevel2 and ObjectALevel3 construct have been called." );
            Assert.That( oB.ConstructCount, Is.GreaterThanOrEqualTo( 3 ), "ObjectB, ObjectBLevel1 and ObjectBLevel2 construct have been called." );

            SimpleObjectsTrace.LogMethod( GetType().GetMethod( "StObjConstruct", BindingFlags.Instance | BindingFlags.NonPublic ) );
            oB.MethofOfBOnLevel2();

            ConstructCount = ConstructCount + 1;
        }

        public override void MethofOfALevel3()
        {
            SimpleObjectsTrace.LogMethod( GetType().GetMethod( "MethofOfALevel3", BindingFlags.Instance | BindingFlags.Public ) );
        }

    }
}
