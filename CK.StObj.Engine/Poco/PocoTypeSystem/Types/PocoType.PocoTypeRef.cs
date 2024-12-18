using CK.Core;
using static CK.Setup.IPocoType;

namespace CK.Setup;


partial class PocoType
{
    /// <summary>
    /// Implements an autonomous <see cref="ITypeRef"/> node.
    /// </summary>
    sealed class PocoTypeRef : ITypeRef
    {
        readonly IPocoType _owner;
        readonly IPocoType _type;
        readonly ITypeRef? _next;
        readonly int _index;

        public ITypeRef? NextRef => _next;

        IPocoType ITypeRef.Owner => _owner;

        IPocoType ITypeRef.Type => _type;

        public int Index => _index;

        internal PocoTypeRef( IPocoType owner, IPocoType t, int index )
        {
            Throw.DebugAssert( "Oblivious back reference is -1.", index >= -1 );
            _owner = owner;
            _type = t;
            _index = index;
            _next = ((PocoType)t.NonNullable).AddBackRef( this );
        }
    }
}
