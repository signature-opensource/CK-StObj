using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;

namespace CK.Setup
{
    public sealed class CachedAssembly
    {
        readonly Assembly _assembly;
        readonly AssemblyName _assemblyName;

        // Initialized on demand.
        ImmutableArray<CustomAttributeData> _customAttributes;
        ImmutableArray<Type> _rawExportedTypes;
        // Initialized by AssemblyCollector.DoAdd.
        internal CKAssemblyKind _kind;
        internal ImmutableArray<CachedAssembly> _rawReferencedAssembly;
        internal IReadOnlySet<CachedAssembly> _ckAssemblies;

        internal CachedAssembly( Assembly assembly, AssemblyName assemblyName, CKAssemblyKind initialKind )
        {
            _assembly = assembly;
            _assemblyName = assemblyName;
            _kind = initialKind;
            _ckAssemblies = ImmutableHashSet<CachedAssembly>.Empty;
            if( initialKind == CKAssemblyKind.None )
            {
                _kind = GetInitialAssemblyType();
            }
            else
            {
                _customAttributes = ImmutableArray<CustomAttributeData>.Empty;
                _rawExportedTypes = ImmutableArray<Type>.Empty;
                _rawReferencedAssembly = ImmutableArray<CachedAssembly>.Empty;
            }
        }

        CKAssemblyKind GetInitialAssemblyType()
        {
            // Initial type detection.
            bool isDefiner = false;
            foreach( var d in CustomAttributes )
            {
                if( d.AttributeType.Name == "IsModelDependentAttribute" ) return CKAssemblyKind.CKAssembly;
                if( d.AttributeType.Name == "IsModelAttribute" ) isDefiner = true;
                if( d.AttributeType.Name == "IsSetupDependencyAttribute" ) return CKAssemblyKind.CKEngine;
            }
            return isDefiner ? CKAssemblyKind.CKAssemblyDefiner : CKAssemblyKind.None;
        }

        /// <summary>
        /// Gets this assembly's simple name.
        /// </summary>
        public string Name => _assemblyName.Name ?? string.Empty;

        /// <summary>
        /// Gets this assembly's full name.
        /// </summary>
        public string FullName => _assemblyName.FullName;

        /// <summary>
        /// Gets the exported types of this assembly.
        /// </summary>
        public ImmutableArray<Type> RawExportedTypes
        {
            get
            {
                if( _rawExportedTypes.IsDefault )
                {
                    _rawExportedTypes = _assembly.GetExportedTypes().ToImmutableArray();
                }
                return _rawExportedTypes;
            }
        }

        /// <summary>
        /// Gets this kind of assembly.
        /// </summary>
        public CKAssemblyKind Kind => _kind;

        /// <summary>
        /// Gets the custom attributes.
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
        /// Gets all the referenced assemblies, regardless of their <see cref="Kind"/> and any <see cref="ExcludeCKAssemblyAttribute"/>.
        /// <para>
        /// This is always empty for <see cref="CKAssemblyKind.Skipped"/> or <see cref="CKAssemblyKind.Excluded"/>.
        /// </para>
        /// </summary>
        public ImmutableArray<CachedAssembly> RawReferencedAssemblies => _rawReferencedAssembly;

        /// <summary>
        /// Gets the <see cref="CKAssemblyKind.CKAssembly"/>, considering any <see cref="ExcludeCKAssemblyAttribute"/>
        /// defined by this assembly.
        /// <para>
        /// This is always empty except for <see cref="CKAssemblyKind.CKAssembly"/>.
        /// </para>
        /// </summary>
        public IReadOnlySet<CachedAssembly> CKAssemblies => _ckAssemblies;
    }
}
