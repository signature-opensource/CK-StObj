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
            Handlers = handlers;
            ECMAStandardReadHandlers = ecmaStandardReadhandlers ?? (IReadOnlyList<IJsonCodeGenHandler>)Array.Empty<IJsonCodeGenHandler>();
        }

        internal void OnPocoInfoAvailable( PocoJsonInfo p )
        {
            PocoJsonInfo = p;
            if( !p.IsECMAStandardCompliant ) ECMAStandardReadHandlers = Array.Empty<IJsonCodeGenHandler>();
        }

        public IPocoJsonInfo PocoJsonInfo { get; private set; }

        public IPocoPropertyInfo PropertyInfo { get; }

        public bool IsJsonUnionType => Handlers.Count > 1;

        public IReadOnlyList<IJsonCodeGenHandler> Handlers { get; }

        public IReadOnlyList<IJsonCodeGenHandler> ECMAStandardReadHandlers { get; private set; }

    }


}
