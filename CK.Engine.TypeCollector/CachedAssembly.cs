using CK.Core;
using CK.Setup;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Numerics;
using System.Reflection;

namespace CK.Engine.TypeCollector
{
    public sealed class CachedAssembly
    {
        readonly Assembly _assembly;
        readonly string _assemblyName;
        readonly string _assemblyFullName;
        readonly DateTime _lastWriteTime;
        readonly bool _isInitialAssembly;

        // Initialized on demand.
        ImmutableArray<CustomAttributeData> _customAttributes;
        ImmutableArray<Type> _allVisibleTypes;
        // Initialized by AssemblyCollector.DoAdd.
        internal AssemblyKind _kind;
        internal ImmutableArray<CachedAssembly> _rawReferencedAssembly;
        internal IReadOnlySet<CachedAssembly> _pFeatures;
        internal IReadOnlySet<CachedAssembly> _allPFeatures;

        internal CachedAssembly( Assembly assembly,
                                 string name,
                                 string fullName,
                                 AssemblyKind initialKind,
                                 DateTime lastWriteTime,
                                 bool isInitialAssembly )
        {
            _assembly = assembly;
            _assemblyName = name;
            _assemblyFullName = fullName;
            _kind = initialKind;
            _lastWriteTime = lastWriteTime;
            _isInitialAssembly = isInitialAssembly;
            _pFeatures = ImmutableHashSet<CachedAssembly>.Empty;
            _allPFeatures = ImmutableHashSet<CachedAssembly>.Empty;
            if( initialKind == AssemblyKind.None )
            {
                _kind = GetInitialAssemblyType();
                Throw.DebugAssert( _kind is AssemblyKind.PFeature
                                    or AssemblyKind.CKEngine
                                    or AssemblyKind.PFeatureDefiner
                                    or AssemblyKind.None );
                // Auto exclusion or post registration assembly.
                if( !isInitialAssembly || _kind == AssemblyKind.Excluded )
                {
                    _allVisibleTypes = ImmutableArray<Type>.Empty;
                    _rawReferencedAssembly = ImmutableArray<CachedAssembly>.Empty;
                }
            }
            else
            {
                Throw.DebugAssert( _kind is AssemblyKind.Skipped or AssemblyKind.Excluded );
                _customAttributes = ImmutableArray<CustomAttributeData>.Empty;
                _allVisibleTypes = ImmutableArray<Type>.Empty;
                _rawReferencedAssembly = ImmutableArray<CachedAssembly>.Empty;
            }
        }

        AssemblyKind GetInitialAssemblyType()
        {
            // Initial type detection. Allocates the CustomAttributes.
            uint found = 0;
            foreach( var d in CustomAttributes )
            {
                // Auto exclusion: nothing more to do.
                // This hides the "multiple error" below but we don't care.
                if( d.AttributeType == typeof( ExcludePFeatureAttribute ) )
                {
                    var excludedName = (string?)d.ConstructorArguments[0].Value;
                    if( excludedName == "this" || excludedName == _assemblyName )
                    {
                        return AssemblyKind.Excluded;
                    }
                }
                // Legacy.
                if( d.AttributeType.Name == "ExcludeFromSetupAttribute" )
                {
                    return AssemblyKind.Excluded;
                }
                if( d.AttributeType.Name == nameof( IsPFeatureAttribute ) || /*Legacy*/d.AttributeType.Name == "IsModelDependentAttribute" ) found |= 1;
                else if( d.AttributeType.Name == nameof( IsCKEngineAttribute ) || /*Legacy*/d.AttributeType.Name == "IsSetupDependencyAttribute" ) found |= 2;
                else if( d.AttributeType.Name == nameof( IsPFeatureDefinerAttribute ) || /*Legacy*/d.AttributeType.Name == "IsModelAttribute" ) found |= 4;
            }
            if( BitOperations.PopCount( found ) > 1 )
            {
                // We cannot reaaly do anything else here... We cannot exclude the assembly, nor choose one at random because there is no sensible choice.
                // This is a totally fucked up assembly: the developper must fix this. 
                Throw.CKException( $"""
                                    Invalid assembly '{_assemblyName}': it contains more than one [IsPFeature], [IsCKEngine] or [IsPFeatureDefiner].
                                    These attributes are mutually exclusive.
                                    """ );
            }
            return found switch { 1 => AssemblyKind.PFeature, 2 => AssemblyKind.CKEngine, 4 => AssemblyKind.PFeatureDefiner, _ => AssemblyKind.None };
        }

        /// <summary>
        /// Gets this assembly's simple name.
        /// </summary>
        public string Name => _assemblyName;

        /// <summary>
        /// Gets this assembly's full name.
        /// </summary>
        public string FullName => _assemblyFullName;

        /// <summary>
        /// Gets the assembly. There should be few reasons to need it.
        /// </summary>
        public Assembly Assembly => _assembly;

        /// <summary>
        /// Gets the last write time of the file if the assembly file exists in <see cref="AppContext.BaseDirectory"/>,
        /// otherwise <see cref="Util.UtcMinValue"/>.
        /// </summary>
        public DateTime LastWriteTimeUtc => _lastWriteTime;

        /// <summary>
        /// Gets the visible types of this assembly (cached <see cref="Assembly.GetExportedTypes()"/>).
        /// <para>
        /// This is always empty when <see cref="AssemblyKind.Excluded"/> or <see cref="AssemblyKind.Skipped"/>
        /// or when <see cref="IsInitialAssembly"/> is false.
        /// </para>
        /// </summary>
        public ImmutableArray<Type> AllVisibleTypes
        {
            get
            {
                if( _allVisibleTypes.IsDefault )
                {
                    _allVisibleTypes = _assembly.GetExportedTypes().ToImmutableArray();
                }
                return _allVisibleTypes;
            }
        }

        /// <summary>
        /// Gets this kind of assembly.
        /// </summary>
        public AssemblyKind Kind => _kind;

        /// <summary>
        /// Gets the custom attributes data (attributes are not instantiated).
        /// </summary>
        public ImmutableArray<CustomAttributeData> CustomAttributes
        {
            get
            {
                if( _customAttributes.IsDefault )
                {
                    _customAttributes = _assembly.CustomAttributes.ToImmutableArray();
                }
                return _customAttributes;
            }
        }

        /// <summary>
        /// Gets all the directly referenced assemblies, regardless of their <see cref="Kind"/> and
        /// any <see cref="ExcludePFeatureAttribute"/> that this assembly can define.
        /// <para>
        /// This is always empty when <see cref="AssemblyKind.Excluded"/> or <see cref="AssemblyKind.Skipped"/>
        /// or when <see cref="IsInitialAssembly"/> is false.
        /// </para>
        /// </summary>
        public ImmutableArray<CachedAssembly> RawReferencedAssemblies => _rawReferencedAssembly;

        /// <summary>
        /// Gets all the <see cref="AssemblyKind.PFeature"/> that this PFeature references. 
        /// <para>
        /// This is always empty when <see cref="AssemblyKind.Excluded"/> or <see cref="AssemblyKind.Skipped"/>
        /// or when <see cref="IsInitialAssembly"/> is false.
        /// </para>
        /// </summary>
        public IReadOnlySet<CachedAssembly> AllPFeatures => _allPFeatures;

        /// <summary>
        /// Gets the curated closure of <see cref="AssemblyKind.PFeature"/>, considering any <see cref="ExcludePFeatureAttribute"/>
        /// defined by this assembly and by referenced ones: a referenced assembly doesn't appear here if it has been excluded by
        /// this assembly or if it is an indirect reference that has been excluded by all the inermediate referencers.
        /// <para>
        /// Stated differently:
        /// <list type="bullet">
        ///     <item>An assembly is excluded if everybody agrees to exclude it.</item>
        ///     <item>A parent assembly can always exclude any of its referenced assemblies, even assemblies that it doesn't reference directly.</item>
        /// </list>
        /// </para>
        /// This is always empty when <see cref="AssemblyKind.Excluded"/> or <see cref="AssemblyKind.Skipped"/>
        /// or when <see cref="IsInitialAssembly"/> is false.
        /// </summary>
        public IReadOnlySet<CachedAssembly> PFeatures => _pFeatures;

        /// <summary>
        /// Gets whether this assembly has been been discovered by the <see cref="AssemblyCollector"/> or is
        /// an assembly that comes from a type registration in <see cref="TypeCollector"/>.
        /// </summary>
        public bool IsInitialAssembly => _isInitialAssembly;
    }
}
