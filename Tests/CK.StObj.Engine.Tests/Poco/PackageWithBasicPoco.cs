#region Proprietary License
/*----------------------------------------------------------------------------
* This file (Tests\CK.StObj.Engine.Tests\SimpleObjects\PackageForAB.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using CK.Core;

namespace CK.StObj.Engine.Tests.Poco
{
    [StObj( ItemKind = DependentItemKindSpec.Container )]
    public class PackageWithBasicPoco : IRealObject
    {
        void StObjConstruct( IPocoFactory<IBasicPoco> f )
        {
            Factory = f;
        }

        public IPocoFactory<IBasicPoco> Factory { get; private set; }

    }
}
