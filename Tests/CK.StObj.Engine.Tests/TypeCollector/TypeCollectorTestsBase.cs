using CK.Core;
using CK.Setup;
using FluentAssertions;
using System;
using System.Collections.Generic;

using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Service.TypeCollector;

public class TypeCollectorTestsBase
{

    public static CKTypeCollector CreateCKTypeCollector( Func<Type, bool>? typeFilter = null )
    {
        Func<IActivityMonitor, Type, bool>? f = null;
        if( typeFilter != null ) f = ( m, t ) => typeFilter( t );
        return new CKTypeCollector( new SimpleServiceContainer(), new DynamicAssembly(), f );
    }

    public static CKTypeCollectorResult CheckSuccess( Action<CKTypeCollector> registerTypes, CKTypeCollector? existing = null )
    {
        bool error = false;
        using( TestHelper.Monitor.OnError( () => error = true ) )
        {
            if( existing == null ) existing = CreateCKTypeCollector();
            registerTypes( existing );
            var r = existing.GetResult( TestHelper.Monitor );
            r.LogErrorAndWarnings( TestHelper.Monitor );
            (r.HasFatalError || error ).Should().Be( false, "There must be no error." );
            return r;
        }
    }

    public static CKTypeCollectorResult CheckFailure( CKTypeCollector c )
    {
        var r = c.GetResult( TestHelper.Monitor );
        r.LogErrorAndWarnings( TestHelper.Monitor );
        r.HasFatalError.Should().Be( true, "There must be at least one fatal error." );
        return r;
    }

}
