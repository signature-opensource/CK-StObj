using CK.Core;
using CK.Setup;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Testing;

/// <summary>
/// Extends <see cref="IBasicTestHelper"/> or <see cref="IMonitorTestHelper"/> with engine related helpers.
/// <para>
/// Extends <see cref="EngineConfiguration"/> objects with configuration helper methods and run methods
/// like <see cref="RunSuccessfullyAsync(EngineConfiguration, bool)"/>.
/// </para>
/// Extends the CKomposable <see cref="EngineResult"/> with <see cref="IStObjMap"/> load capabilites
/// and <see cref="AutomaticServices"/> creation.
/// </summary>
public static partial class EngineTestHelperExtensions
{
    /// <summary>
    /// Creates a default <see cref="EngineConfiguration"/> with the <see cref="EngineConfiguration.FirstBinPath"/> that has
    /// its <see cref="BinPathConfiguration.Path"/> set to the <see cref="IBasicTestHelper.ClosestSUTProjectFolder"/> and its
    /// <see cref="BinPathConfiguration.ProjectPath"/> sets to this <see cref="IBasicTestHelper.TestProjectFolder"/>.
    /// </summary>
    /// <param name="helper">This helper.</param>
    /// <param name="generateSourceFiles">False to not generate source file.</param>
    /// <param name="compileOption">See <see cref="BinPathConfiguration.CompileOption"/>.</param>
    /// <returns>A default configuration.</returns>
    public static EngineConfiguration CreateDefaultEngineConfiguration( this IBasicTestHelper helper, bool generateSourceFiles = true, CompileOption compileOption = CompileOption.Compile )
    {
        var config = new EngineConfiguration();
        var sutFolder = helper.ClosestSUTProjectFolder;
        config.FirstBinPath.Path = sutFolder.Combine( helper.PathToBin );
        config.FirstBinPath.ProjectPath = helper.TestProjectFolder;
        config.FirstBinPath.CompileOption = compileOption;
        config.FirstBinPath.GenerateSourceFiles = generateSourceFiles;
        return config;
    }

    /// <summary>
    /// Ensures that there is no registration errors at the <see cref="StObjCollector"/> and returns a successful <see cref="StObjCollectorResult"/>.
    /// </summary>
    /// <param name="helper">This helper.</param>
    /// <param name="types">The set of types to collect.</param>
    /// <returns>The successful collector result.</returns>
    public static StObjCollectorResult GetSuccessfulCollectorResult( this IMonitorTestHelper helper, IEnumerable<Type> types )
    {
        var c = new StObjCollector( new SimpleServiceContainer() );
        c.RegisterTypes( helper.Monitor, types );
        StObjCollectorResult r = c.GetResult( helper.Monitor );
        r.HasFatalError.ShouldBe( false, "There must be no error." );
        return r;
    }

    /// <summary>
    /// Ensures that there are registration errors or a fatal error during the creation of the <see cref="StObjCollectorResult"/>
    /// and returns it if it has been created on error.
    /// <para>
    /// This methods expects at least a substring that must appear in a Error or Fatal emitted log. Testing a failure
    /// should always challenge that the failure cause is what it should be.
    /// To disable this (but this is NOT recommended), <paramref name="message"/> may be set to the empty string.
    /// </para>
    /// </summary>
    /// <param name="helper">This helper.</param>
    /// <param name="types">The set of types to collect.</param>
    /// <param name="message">Expected error or fatal message substring that must be emitted.</param>
    /// <param name="otherMessages">More fatal messages substring that must be emitted.</param>
    /// <returns>The failed collector result or null if the error prevented its creation.</returns>
    public static StObjCollectorResult? GetFailedCollectorResult( this IMonitorTestHelper helper, IEnumerable<Type> types, string message, params string[] otherMessages )
    {
        var c = new StObjCollector();
        c.RegisterTypes( helper.Monitor, types );
        if( c.FatalOrErrors.Count != 0 )
        {
            helper.Monitor.Error( $"GetFailedCollectorResult: {c.FatalOrErrors.Count} fatal or error during StObjCollector registration." );
            CheckExpectedMessages( c.FatalOrErrors, message, otherMessages );
            return null;
        }
        var r = c.GetResult( helper.Monitor );
        r.HasFatalError.ShouldBe( true, "GetFailedCollectorResult: StObjCollector.GetResult() must have failed with at least one fatal error." );
        CheckExpectedMessages( c.FatalOrErrors, message, otherMessages );
        return r;
    }

    static void CheckExpectedMessages( IEnumerable<string> fatalOrErrors, string message, IEnumerable<string>? otherMessages )
    {
        CheckMessage( fatalOrErrors, message );
        if( otherMessages != null )
        {
            foreach( var m in otherMessages ) CheckMessage( fatalOrErrors, m );
        }

        static void CheckMessage( IEnumerable<string> fatalOrErrors, string m )
        {
            if( !String.IsNullOrEmpty( m ) )
            {
                m = m.ReplaceLineEndings();
                var errors = fatalOrErrors.Select( m => m.ReplaceLineEndings() );
                errors.Any( e => e.Contains( m, StringComparison.OrdinalIgnoreCase ) )
                    .ShouldBeTrue( $"Expected '{m}' to be found in: {Environment.NewLine}{errors.Concatenate( Environment.NewLine )}" );
            }
        }
    }

}
