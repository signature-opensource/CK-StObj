using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Engine.TypeCollector
{

    class CachedType : ICachedType
    {
        readonly Type _type;
        readonly CachedAssembly _assembly;

        public CachedType( Type type, CachedAssembly assembly )
        {
            _type = type;
            _assembly = assembly;
        }

        public Type Type => _type;

        public CachedAssembly Assembly => _assembly;
    }
}
