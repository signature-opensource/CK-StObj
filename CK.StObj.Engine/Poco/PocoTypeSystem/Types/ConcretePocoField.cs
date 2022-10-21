namespace CK.Setup
{
    sealed class ConcretePocoField : IConcretePocoField
    {
        readonly IPocoPropertyInfo _p;
        readonly IPocoType _type;
        readonly DefaultValueInfo _defInfo;

        public ConcretePocoField( IPocoPropertyInfo p, IPocoType type, bool readOnly, bool byRef, IPocoFieldDefaultValue? defaultValue )
        {
            _p = p;
            _type = type;
            _defInfo = defaultValue != null ? new DefaultValueInfo(defaultValue) : type.DefaultValueInfo;
            PrivateFieldName = $"_v{Index}";
            IsReadOnly = readOnly;
            IsByRef = byRef;
        }

        public int Index => _p.Index;

        public string Name => _p.Name;  

        public IPocoPropertyInfo Property => _p;

        public string PrivateFieldName { get; }

        public IPocoType Type => _type;

        public DefaultValueInfo DefaultValueInfo => _defInfo;

        public bool IsReadOnly { get; }

        public bool IsByRef { get; }
    }
}
