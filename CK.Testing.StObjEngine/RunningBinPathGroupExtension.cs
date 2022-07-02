using CK.Core;
using CK.Setup;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.Testing.StObjEngine
{
    /// <summary>
    /// Extends <see cref="IRunningBinPathGroup"/>.
    /// </summary>
    public static class RunningBinPathGroupExtension
    {
        /// <summary>
        /// Loads the <see cref="IStObjMap"/> from <see cref="IRunningBinPathGroup.RunSignature"/> SHA1 from
        /// already available maps (see <see cref="StObjContextRoot.Load(SHA1Value, IActivityMonitor?)"/>)
        /// or from the <see cref="IRunningBinPathGroup.GeneratedAssembly"/>.
        /// <para>
        /// This must not be called on the <see cref="IRunningBinPathGroup.IsUnifiedPure"/> otherwise an <see cref="InvalidOperationException"/>
        /// is thrown.
        /// </para>
        /// </summary>
        /// <param name="g">This group from which the map must be obtain.</param>
        /// <param name="embeddedIfPossible">
        /// False to skip an available map and load it from the generated assembly.
        /// By default, the map is searched in available ones before loading the assembly.
        /// </param>
        /// <returns>The map.</returns>
        public static IStObjMap LoadStObjMap( this IRunningBinPathGroup g, bool embeddedIfPossible = true )
        {
            Throw.CheckState( !g.IsUnifiedPure );
            IStObjMap? map = null;
            if( embeddedIfPossible )
            {
                IStObjMap? embedded = StObjContextRoot.Load( g.RunSignature, TestHelper.Monitor );
                if( embedded != null )
                {
                    TestHelper.Monitor.Info( embedded == null ? "No embedded generated source code." : "Embedded generated source code is available." );
                }
            }
            if( map == null )
            {
                g.GeneratedAssembly.Exists().Should().BeTrue( $"The assembly '{g.GeneratedAssembly.Path}' should have been generated." );
                var a = AssemblyLoadContext.Default.LoadFromAssemblyPath( g.GeneratedAssembly.Path );
                map = StObjContextRoot.Load( a, TestHelper.Monitor );
                map.Should().NotBeNull( $"The assembly '{g.GeneratedAssembly.Path}' should be loadable." );
            }
            return map!;
        }
    }
}
