using System;

namespace CK.Engine.TypeCollector
{
    public interface ICachedType
    {
        Type Type { get; }

        CachedAssembly Assembly { get; }
    }
}
