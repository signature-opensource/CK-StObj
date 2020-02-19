#region Proprietary License
/*----------------------------------------------------------------------------
* This file (Tests\CK.StObj.Engine.Tests\SimpleObjects\PackageForAB.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System.Reflection;
using CK.Core;
using NUnit.Framework;

namespace CK.StObj.Engine.Tests.SimpleObjects
{
    public interface IAmNotHere : IRealObject { }

    [StObj( ItemKind = DependentItemKindSpec.Container )]
    public class PackageForAB : IRealObject
    {
        public int ConstructCount { get; protected set; }

        // Adds an optional parameter otherwise parameter less StObjConstruct are not called.
        void StObjConstruct( IAmNotHere opt = null )
        {
            Assert.That( ConstructCount, Is.EqualTo( 0 ), "First construct." );
            SimpleObjectsTrace.LogMethod( GetType().GetMethod( "StObjConstruct", BindingFlags.Instance | BindingFlags.NonPublic ) );
            ConstructCount = ConstructCount + 1;
        }
        
    }
}
