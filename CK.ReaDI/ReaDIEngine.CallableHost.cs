using CK.Engine.TypeCollector;
using System.Collections.Generic;

namespace CK.Core;

public sealed partial class ReaDIEngine
{
    sealed class CallableHost
    {
        readonly IReaDIHandler _handler;
        OldCallable? _head;

        public CallableHost( IReaDIHandler handler )
        {
            _handler = handler;
        }

        internal IReaDIHandler Handler => _handler;

        internal OldCallable? Head => _head; 

        public OldCallable AddCallable( ICachedMethodInfo method )
        {
            var c = new OldCallable( this, method );
            return _head = c;
        }
    }
}
