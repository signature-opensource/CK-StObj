using CK.Core;
using System;

namespace CK.Setup
{
    sealed class PrimaryPocoField : IPrimaryPocoField
    {
        readonly IPocoPropertyInfo _p;
        readonly IPocoType _type;
        readonly PocoType.PrimaryPocoType _owner;
        readonly DefaultValueInfo _defInfo;

        public PrimaryPocoField( IPocoPropertyInfo p,
                                 IPocoType type,
                                 string fieldTypeName,
                                 bool hasSetter,
                                 PocoType.PrimaryPocoType owner,
                                 bool isByRef,
                                 IPocoFieldDefaultValue? defaultValue )
        {
            _p = p;
            _type = type;
            _defInfo = defaultValue != null ? new DefaultValueInfo(defaultValue) : type.DefaultValueInfo;
            PrivateFieldName = $"_v{Index}";
            HasSetter = hasSetter;
            FieldTypeCSharpName = fieldTypeName;
            _owner = owner;
            IsByRef = isByRef;
        }

        public IPrimaryPocoType Owner => _owner;

        ICompositePocoType IPocoField.Owner => _owner;

        public int Index => _p.Index;

        public string Name => _p.Name;  

        public IPocoPropertyInfo Property => _p;

        public string PrivateFieldName { get; }

        public IPocoType Type => _type;

        public DefaultValueInfo DefaultValueInfo => _defInfo;

        public bool HasSetter { get; }

        public bool IsByRef { get; }

        public string FieldTypeCSharpName { get; }

        public override string ToString() => $"{_type} {Name}";
    }
}
