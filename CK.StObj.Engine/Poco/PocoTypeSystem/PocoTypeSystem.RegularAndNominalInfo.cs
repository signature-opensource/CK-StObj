using System.Collections.Generic;
using System.Diagnostics;

namespace CK.Setup
{
    public sealed partial class PocoTypeSystem
    {
        readonly Dictionary<string, RegularAndNominalInfo> _regNominalCollections;

        sealed class RegularAndNominalInfo : ICollectionPocoType.IRegularAndNominalInfo
        {
            public RegularAndNominalInfo( string typeName, IReadOnlyList<IPocoType> itemTypes, int index )
            {
                TypeName = typeName;
                ItemTypes = itemTypes;
                Index = index;
            }

            public string TypeName { get; }

            public IReadOnlyList<IPocoType> ItemTypes { get; }

            public int Index { get; }
        }

        internal ICollectionPocoType.IRegularAndNominalInfo RegisterPocoRegularAndNominal( string name, ICollectionPocoType t )
        {
            Debug.Assert( t.Kind != PocoTypeKind.Array );
            Debug.Assert( t.Kind == PocoTypeKind.Dictionary
                          || (t.ItemTypes[0].Type.IsValueType || t.ItemTypes[0].IsNullable),
                          "For list or set, the item is a value type or a nullable reference type." );
            Debug.Assert( t.Kind != PocoTypeKind.Dictionary
                          || ((t.ItemTypes[0].Type.IsValueType || !t.ItemTypes[0].IsNullable)
                               && (t.ItemTypes[1].Type.IsValueType || t.ItemTypes[1].IsNullable) ),
                          "For dictionary, the key is a value type or a non nullable reference type and the value is a value type or a nullable reference type." );
            if( !_regNominalCollections.TryGetValue( name, out var info ) )
            {
                info = new RegularAndNominalInfo( name, t.ItemTypes, _regNominalCollections.Count );
                _regNominalCollections.Add( name, info );
            }
            Debug.Assert( info.ItemTypes[0] == t.ItemTypes[0]
                          && (t.ItemTypes.Count == 1 || info.ItemTypes[1] == t.ItemTypes[1]) );
            return info;
        }

    }

}
