using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace CK.Setup
{
    partial class PocoRegistrar
    {
        sealed class PocoPropertyImpl : IPocoPropertyImpl
        {
            readonly UnionType? _unionType;
            readonly PocoPropertyKind _kind;

            public PocoPropertyImpl( IPocoPropertyInfo p,
                                     PropertyInfo i,
                                     bool isReadOnly,
                                     PocoPropertyKind kind,
                                     NullableTypeTree type,
                                     UnionType? unionType )
            {
                Debug.Assert( i.DeclaringType != null );
                PocoProperty = p;
                Info = i;
                NullableTypeTree = type;
                _unionType = unionType;
                IsReadOnly = isReadOnly;
            }

            public IPocoPropertyInfo PocoProperty { get; }

            public PropertyInfo Info { get; }

            public Type DeclaringType => Info.DeclaringType!;

            public string Name => Info.Name;

            public bool IsReadOnly { get; }

            public PocoPropertyKind PocoPropertyKind => _kind;

            public NullableTypeTree NullableTypeTree { get; }

            IEnumerable<NullableTypeTree> IPocoPropertyImpl.UnionTypes => _unionType?.Types ?? (IEnumerable<NullableTypeTree>)Array.Empty<NullableTypeTree>();

            public UnionType? UnionTypes => _unionType;

            /// <summary>
            /// Returns "Interface.Name" (without the property type).
            /// </summary>
            public override string ToString() => $"{DeclaringType}.{Name}";
        }
    }
}
