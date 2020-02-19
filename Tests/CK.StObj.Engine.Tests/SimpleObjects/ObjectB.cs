#region Proprietary License
/*----------------------------------------------------------------------------
* This file (Tests\CK.StObj.Engine.Tests\SimpleObjects\ObjectB.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System.Reflection;
using CK.Core;
using NUnit.Framework;

namespace CK.StObj.Engine.Tests.SimpleObjects
{
    public class ObjectB : IRealObject
    {
        IAbstractionA _a;

        public int ConstructCount { get; protected set; }

        void StObjConstruct( [Container]PackageForAB package, IAbstractionA a )
        {
            Assert.That( ConstructCount, Is.EqualTo( 0 ), "First construct." );
            Assert.That( a.ConstructCount, Is.GreaterThanOrEqualTo( 1 ), "At least ObjectA.StObjConstruct have been called.");
            Assert.That( package.ConstructCount, Is.GreaterThanOrEqualTo( 1 ), "At least PackageForAB.StObjConstruct has been called.");
            
            SimpleObjectsTrace.LogMethod( GetType().GetMethod( "StObjConstruct", BindingFlags.Instance | BindingFlags.NonPublic ) );
            _a = a;

            ConstructCount = ConstructCount + 1;
        }
    }
}
