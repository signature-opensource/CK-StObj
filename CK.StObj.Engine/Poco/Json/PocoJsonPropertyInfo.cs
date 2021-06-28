using System;
using System.Collections.Generic;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace CK.Setup.Json
{
    sealed class PocoJsonPropertyInfo : IPocoJsonPropertyInfo
    {
        internal PocoJsonPropertyInfo( IPocoPropertyInfo p, IReadOnlyList<IJsonCodeGenHandler> handlers, IReadOnlyList<IJsonCodeGenHandler>? ecmaStandardReadhandlers )
        {
            PropertyInfo = p;
            AllHandlers = handlers;
            ECMAStandardHandlers = ecmaStandardReadhandlers ?? (IReadOnlyList<IJsonCodeGenHandler>)Array.Empty<IJsonCodeGenHandler>();
        }

        internal void OnPocoInfoAvailable( PocoJsonInfo p )
        {
            PocoJsonInfo = p;
            if( !p.IsECMAStandardCompliant ) ECMAStandardHandlers = Array.Empty<IJsonCodeGenHandler>();
        }

        public IPocoJsonInfo PocoJsonInfo { get; private set; }

        public IPocoPropertyInfo PropertyInfo { get; }

        public bool IsJsonUnionType => AllHandlers.Count > 1;

        public IReadOnlyList<IJsonCodeGenHandler> AllHandlers { get; }

        public IReadOnlyList<IJsonCodeGenHandler> ECMAStandardHandlers { get; private set; }

    }


}
