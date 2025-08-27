using CK.Core;
using CK.Setup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Demo;


public abstract class EngineAspect : IReaDIHandler
{
    readonly EngineAspectConfiguration _configuration;

    protected EngineAspect( EngineAspectConfiguration configuration )
    {
        _configuration = configuration;
    }

    public EngineAspectConfiguration Configuration => _configuration;
}
