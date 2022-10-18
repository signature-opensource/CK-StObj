using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace CK.Setup
{
    public sealed class RecordField : IRecordPocoField
    {
        [AllowNull]
        IPocoType _type;

        public RecordField( int index, string? rawName, IPocoFieldDefaultValue? defaultValue = null )
        {
            Index = index;
            if( rawName == null )
            {
                Name = $"Item{index + 1}";
                IsUnnamed = true;
            }
            else
            {
                Name = rawName;
            }
            DefaultValue = defaultValue;
        }

        public int Index { get; }

        public string Name { get; }

        public IPocoFieldDefaultValue? DefaultValue { get; private set; }

        public bool IsUnnamed { get; }

        public IPocoType Type
        {
            get => _type;
            internal set
            {
                Debug.Assert( _type == null && value != null );
                _type = value;
                // If we have no default and the type is a non nullable string, we
                // set the empty string as the default.
                if( DefaultValue == null && !value.IsNullable && value.Kind == PocoTypeKind.Basic && value.Type == typeof( string ) )
                {
                    DefaultValue = PocoFieldDefaultValue.StringDefault;
                }
            }
        }
    }
}
