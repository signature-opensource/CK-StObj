using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using CK.CodeGen;
using CK.CodeGen.Abstractions;

namespace CK.Setup
{
    /// <summary>
    /// Implements <see cref="IDynamicAssembly"/>.
    /// </summary>
    public class DynamicAssembly : IDynamicAssembly
    {
        int _typeID;
        readonly IDictionary _memory;

        /// <summary>
        /// Initializes a new <see cref="DynamicAssembly"/>.
        /// </summary>
        public DynamicAssembly()
        {
            var name = Guid.NewGuid().ToString();
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly( new AssemblyName( name ), AssemblyBuilderAccess.Run );
            StubModuleBuilder = assemblyBuilder.DefineDynamicModule( name );

            _memory = new System.Collections.Specialized.HybridDictionary();

            SourceModules = new List<ICodeGeneratorModule>();
            var ws = CodeWorkspace.Create();
            ws.Global.Append( "[assembly:CK.Setup.ExcludeFromSetup()]" ).NewLine();
            DefaultGenerationNamespace = ws.Global.FindOrCreateNamespace( "CK._g" );
        }

        /// <inheritdoc />
        public IDictionary Memory => _memory;

        /// <inheritdoc />
        public ModuleBuilder StubModuleBuilder { get; }

        /// <inheritdoc />
        public INamespaceScope DefaultGenerationNamespace { get; }

        /// <inheritdoc />
        public IList<ICodeGeneratorModule> SourceModules { get; }

        /// <inheritdoc />
        public string NextUniqueNumber() => (++_typeID).ToString();

    } 

}
