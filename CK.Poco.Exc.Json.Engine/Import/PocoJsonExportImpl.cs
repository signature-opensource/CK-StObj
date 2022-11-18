using CK.CodeGen;
using CK.Core;
using System;

namespace CK.Setup.PocoJson
{
    public class PocoJsonImportImpl : CSCodeGeneratorType
    {
        public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
        {
            return CSCodeGenerationResult.Success;
        }
    }
}
