using CK.Engine.TypeCollector;
using System.Collections.Generic;

namespace CK.Core;

public sealed partial class ReaDIEngine
{
    sealed class CallableHost
    {
        readonly IReaDIHandler _handler;
        Callable? _head;

        public CallableHost( IReaDIHandler handler )
        {
            _handler = handler;
        }

        internal IReaDIHandler Handler => _handler;

        internal Callable? Head => _head; 

        public Callable AddCallable( ICachedMethodInfo method )
        {
            var c = new Callable( this, method );
            return _head = c;
        }
    }
}
