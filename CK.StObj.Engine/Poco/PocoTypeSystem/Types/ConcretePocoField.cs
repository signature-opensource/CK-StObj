namespace CK.Setup
{
    sealed class ConcretePocoField : IConcretePocoField
    {
        readonly IPocoPropertyInfo _p;
        readonly IPocoType _type;
        readonly IPocoFieldDefaultValue? _defaultValue;

        public ConcretePocoField( IPocoPropertyInfo p, IPocoType type, bool readOnly, IPocoFieldDefaultValue? defaultValue )
        {
            _p = p;
            _type = type;
            // If we have no default and the type is a non nullable string, we
            // set the empty string as the default.
            if( (_defaultValue = defaultValue) == null
                && !type.IsNullable && type.Kind == PocoTypeKind.Basic && type.Type == typeof( string ) )
            {
                _defaultValue = PocoFieldDefaultValue.StringDefault;
            }
            PrivateFieldName = $"_v{Index}";
            IsCtorInstantiated = readOnly && defaultValue == null;
        }

        public int Index => _p.Index;

        public string Name => _p.Name;  

        public IPocoFieldDefaultValue? DefaultValue => _defaultValue;

        public IPocoPropertyInfo Property => _p;

        public string PrivateFieldName { get; }

        public IPocoType Type => _type;

        public bool IsCtorInstantiated { get; }
    }
}
