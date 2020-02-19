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
        readonly IDictionary<string, object> _primaryRunCache;
        readonly Func<string, object> _secondaryRunAccessor;

        /// <summary>
        /// Initializes a new <see cref="DynamicAssembly"/>.
        /// </summary>
        DynamicAssembly()
        {
            var name = Guid.NewGuid().ToString();
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly( new AssemblyName( name ), AssemblyBuilderAccess.Run );
            StubModuleBuilder = assemblyBuilder.DefineDynamicModule( name );

            _memory = new Dictionary<object, object>();

            SourceModules = new List<ICodeGeneratorModule>();
            var ws = CodeWorkspace.Create();
            ws.Global.Append( "[assembly:CK.Setup.ExcludeFromSetup()]" ).NewLine();
            DefaultGenerationNamespace = ws.Global.FindOrCreateNamespace( "CK._g" );
        }

        /// <summary>
        /// Initializes a new <see cref="DynamicAssembly"/> for a primary run.
        /// </summary>
        /// <param name="primaryRunCache">The cache that will be filled.</param>
        public DynamicAssembly( IDictionary<string, object> primaryRunCache )
            : this()
        {
            if( primaryRunCache == null ) throw new ArgumentNullException( nameof( primaryRunCache ) );
            _primaryRunCache = primaryRunCache;
        }

        /// <summary>
        /// Initializes a new <see cref="DynamicAssembly"/> for a secondary run.
        /// </summary>
        /// <param name="secondaryRunAccessor">The function that retrieves primary run result.</param>
        public DynamicAssembly( Func<string, object> secondaryRunAccessor )
            : this()
        {
            if( secondaryRunAccessor == null ) throw new ArgumentNullException( nameof( secondaryRunAccessor ) );
            _secondaryRunAccessor = secondaryRunAccessor;
        }

        /// <summary>
        /// Gets a shared dictionary associated to the dynamic assembly. 
        /// Methods that generate code can rely on this to store shared information as required by
        /// their generation process.
        /// </summary>
        public IDictionary Memory => _memory;

        /// <summary>
        /// Gets the <see cref="ModuleBuilder"/> for this <see cref="DynamicAssembly"/>.
        /// </summary>
        public ModuleBuilder StubModuleBuilder { get; }

        /// <summary>
        /// Gets the default name space for this <see cref="IDynamicAssembly"/>
        /// into which code should be generated: this is "CK._g".
        /// Note that nothing prevents the <see cref="INamedScope.Workspace"/> to be used and other
        /// namespaces to be created.
        /// </summary>
        public INamespaceScope DefaultGenerationNamespace { get; }

        /// <summary>
        /// Gets the source modules for this <see cref="IDynamicAssembly"/>.
        /// </summary>
        public IList<ICodeGeneratorModule> SourceModules { get; }

        /// <summary>
        /// Provides a new unique number that can be used for generating unique names inside this dynamic assembly.
        /// </summary>
        /// <returns>A unique number.</returns>
        public string NextUniqueNumber() => (++_typeID).ToString();

        /// <summary>
        /// Gets whether this is a secondary run or the primary run.
        /// </summary>
        public bool IsSecondaryRun => _secondaryRunAccessor != null;

        /// <summary>
        /// Gets an object created by the first run: this must be called only when <see cref="IsSecondaryRun"/> is true.
        /// The key must exist otherwise a <see cref="KeyNotFoundException"/> is throw.
        /// </summary>
        /// <param name="key">Key of the cached result.</param>
        public object GetPrimaryRunResult( string key )
        {
            if( _secondaryRunAccessor == null ) throw new InvalidOperationException();
            return _secondaryRunAccessor( key );
        }

        /// <summary>
        /// Sets an object during the first run: this must be called only when <see cref="IsSecondaryRun"/> is false.
        /// </summary>
        /// <param name="key">Key of the object to cache.</param>
        /// <param name="o">The object to cache.</param>
        /// <param name="addOrUpdate">True to add or update, false to throw an exception if the key already exists.</param>
        public void SetPrimaryRunResult( string key, object o, bool addOrUpdate )
        {
            if( _primaryRunCache == null ) throw new InvalidOperationException();
            if( addOrUpdate ) _primaryRunCache[key] = o;
            else _primaryRunCache.Add( key, o );
        }

    } 

}
