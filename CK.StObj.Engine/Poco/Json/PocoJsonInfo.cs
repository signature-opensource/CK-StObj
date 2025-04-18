using System;
using System.Collections.Generic;

namespace CK.Setup.Json
{
    sealed class PocoJsonInfo : IPocoJsonInfo
    {
        internal PocoJsonInfo( IPocoFamilyInfo i, bool isECMAStandardCompliant, IReadOnlyList<PocoJsonPropertyInfo> properties )
        {
            PocoInfo = i;
            IsECMAStandardCompliant = isECMAStandardCompliant;
            foreach( var p in properties ) p.OnPocoInfoAvailable( this );
            JsonProperties = properties;
        }

        public IPocoFamilyInfo PocoInfo { get; }

        public bool IsECMAStandardCompliant { get; }

        public IReadOnlyList<IPocoJsonPropertyInfo> JsonProperties { get; }
    }

}
