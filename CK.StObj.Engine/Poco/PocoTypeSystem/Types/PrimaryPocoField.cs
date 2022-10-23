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
                                 bool readOnly,
                                 PocoType.PrimaryPocoType owner,
                                 bool byRef,
                                 IPocoFieldDefaultValue? defaultValue )
        {
            _p = p;
            _type = type;
            _defInfo = defaultValue != null ? new DefaultValueInfo(defaultValue) : type.DefaultValueInfo;
            PrivateFieldName = $"_v{Index}";
            IsReadOnly = readOnly;
            _owner = owner;
            IsByRef = byRef;
        }

        public IPrimaryPocoType Owner => _owner;

        ICompositePocoType IPocoField.Owner => _owner;

        public int Index => _p.Index;

        public string Name => _p.Name;  

        public IPocoPropertyInfo Property => _p;

        public string PrivateFieldName { get; }

        public IPocoType Type => _type;

        public DefaultValueInfo DefaultValueInfo => _defInfo;

        public bool IsReadOnly { get; }

        public bool IsByRef { get; }

        public override string ToString() => $"{_type} {Name}";
    }
}
