using CK.CodeGen;
using System.Collections.Generic;
using System;
using System.Diagnostics;

namespace CK.Setup
{
    partial class PocoRegistrar
    {
        sealed class PocoTypeInfo
        {
            public PocoTypeInfo( NullableTypeTree type )
            {
                NullableTypeTree = type;
            }

            public NullableTypeTree NullableTypeTree { get; }
        }

        readonly Dictionary<Type, PocoTypeInfo> _valueTypes;

        public PocoTypeInfo GetValueTupleInfo( NullableTypeTree tree )
        {
            Debug.Assert( tree.Kind.IsTupleType() );

        }

    }
}
