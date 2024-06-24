
using CK.Core;
using CK.Engine.TypeCollector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace CK.Setup
{

    public static class EngineConfigurationExtensions
    {

        public static EngineResult Run( this EngineConfiguration configuration, IActivityMonitor monitor )
        {
            if( !configuration.NormalizeConfiguration( monitor ) )
            {
                return new EngineResult( false, null );
            }
            var groups = BinPathTypeGroup.CreateBinPathTypeGroups( monitor, configuration );
            if( groups == null )
            {
                return new EngineResult( false, null );
            }
            // Tempoary use of the good old StObjEngine.
            var engine = new StObjEngine( monitor, configuration, groups );
            return new EngineResult( engine.NewRun() );
        }

    }
}
