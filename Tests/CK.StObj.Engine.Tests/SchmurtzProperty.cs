#region Proprietary License
/*----------------------------------------------------------------------------
* This file (Tests\CK.StObj.Engine.Tests\SchmurtzProperty.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System;
using CK.Core;

namespace CK.StObj.Engine.Tests
{
    /// <summary>
    /// The Shmurtz works as a collector of where the propagated property comes from (thanks to its support of CK.Core.IMergeable interface).
    /// It is used by StObjPropertiesTests as well as to test Ambient properties.
    /// </summary>
    public class SchmurtzProperty : IMergeable
    {
        public string Schurmtz;

        public SchmurtzProperty( string s )
        {
            Schurmtz = s;
        }

        bool IMergeable.Merge( object source, IServiceProvider services )
        {
            SchmurtzProperty s = (SchmurtzProperty)source;
            Schurmtz = s.Schurmtz + " => " + Schurmtz;
            return true;
        }

        public override string ToString()
        {
            return Schurmtz;
        }
    }
}
