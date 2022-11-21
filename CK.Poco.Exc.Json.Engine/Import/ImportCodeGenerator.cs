using CK.CodeGen;
using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization.Metadata;

namespace CK.Setup.PocoJson
{
    class ImportCodeGenerator
    {
        readonly ITypeScope _pocoDirectory;
        readonly IPocoTypeSystem _typeSystem;
        readonly ICSCodeGenerationContext _generationContext;

        public ImportCodeGenerator( ITypeScope pocoDirectory, IPocoTypeSystem typeSystem, ICSCodeGenerationContext generationContext )
        {
            _pocoDirectory = pocoDirectory;
            _typeSystem = typeSystem;
            _generationContext = generationContext;
        }

        public bool Run( IActivityMonitor monitor )
        {
            return true;
        }

    }
}
