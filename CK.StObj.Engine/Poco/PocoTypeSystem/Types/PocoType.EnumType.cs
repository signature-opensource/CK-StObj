using CK.CodeGen;
using CK.Core;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CK.Setup
{
    partial class PocoType
    {
        /// <summary>
        /// This can return an invalid empty enum and error is logged.
        /// When invalid, the DefaultValueInfo is DefaultValueInfo.Disallowed, the Values are empty
        /// and DefaultValueName is the empty string.
        /// </summary>
        internal static EnumType CreateEnum( IActivityMonitor monitor,
                                             PocoTypeSystemBuilder s,
                                             Type tNotNull,
                                             Type tNull,
                                             IPocoType underlyingType,
                                             ExternalNameAttribute? externalName )
        {
            Debug.Assert( tNotNull.IsEnum );
            return new EnumType( monitor, s, tNotNull, tNull, underlyingType, externalName );
        }

        internal sealed class EnumType : PocoType, IEnumPocoType, IPocoType.ITypeRef
        {
            sealed class Null : NullValueType, IEnumPocoType
            {
                [AllowNull] internal string _extOrCSName;

                public Null( IPocoType notNullable, Type type )
                    : base( notNullable, type )
                {
                }

                new EnumType NonNullable => Unsafe.As<EnumType>( base.NonNullable );

                public IPocoType UnderlyingType => NonNullable.UnderlyingType.Nullable;

                public IReadOnlyDictionary<string, object> Values => NonNullable.Values;

                IEnumPocoType IEnumPocoType.NonNullable => NonNullable;

                IEnumPocoType IEnumPocoType.Nullable => this;

                INamedPocoType INamedPocoType.Nullable => this;
                INamedPocoType INamedPocoType.NonNullable => NonNullable;

                public ExternalNameAttribute? ExternalName => NonNullable.ExternalName;

                public string ExternalOrCSharpName => _extOrCSName;

                public string DefaultValueName => NonNullable.DefaultValueName;
            }

            readonly IPocoType _underlyingType;
            readonly IPocoType.ITypeRef? _nextRef;
            readonly ExternalNameAttribute? _externalName;
            readonly string _defaultValueName;
            readonly DefaultValueInfo _defInfo;
            readonly IReadOnlyDictionary<string, object> _values;

            public EnumType( IActivityMonitor monitor,
                             PocoTypeSystemBuilder s,
                             Type tNotNull,
                             Type tNull,
                             IPocoType underlyingType,
                             ExternalNameAttribute? externalName )
                : base( s,
                        tNotNull,
                        tNotNull.ToCSharpName(),
                        PocoTypeKind.Enum,
                        t => new Null( t, tNull ) )
            {
                Debug.Assert( underlyingType.Kind == PocoTypeKind.Basic );
                _underlyingType = underlyingType;
                if( externalName != null )
                {
                    _externalName = externalName;
                    Nullable._extOrCSName = externalName.Name + '?';
                }
                else
                {
                    Nullable._extOrCSName = Nullable.CSharpName;
                }
                _nextRef = ((PocoType)underlyingType.NonNullable).AddBackRef( this );
                _defInfo = CheckValidAndComputeDefaultValueInfo( monitor, tNotNull, out _values, out _defaultValueName );
            }

            DefaultValueInfo CheckValidAndComputeDefaultValueInfo( IActivityMonitor monitor,
                                                                   Type tNotNull,
                                                                   out IReadOnlyDictionary<string, object> values,
                                                                   out string defaultValueName )
            {
                // [Doc] The elements of the array are sorted by the binary values (that is, the unsigned values)
                //       of the enumeration constants.
                // 
                // => This is perfect for us: if 0 is defined (that is the "normal" default), then it will be
                //    the first value even if negative exist.
                defaultValueName = string.Empty;
                Array vals = tNotNull.GetEnumValues();
                if( vals.Length == 0 )
                {
                    monitor.Error( $"Enum type '{CSharpName}' is empty. Empty enum are not valid in a Poco Type System." );
                    values = ImmutableDictionary<string, object>.Empty;
                    return DefaultValueInfo.Disallowed;
                }
                var names = tNotNull.GetEnumNames();
                // Throws if .NET is broken...
                Throw.CheckState( vals.Length == names.Length );
                defaultValueName = names[0];
                var defValue = vals.GetValue( 0 );
                // Throws if .NET is broken...
                Throw.CheckState( defaultValueName != null && defValue != null );
                var d = new Dictionary<string, object>( names.Length ) { { defaultValueName, defValue } };
                for( int i = 1; i < names.Length; ++i )
                {
                    var o = vals.GetValue( i );
                    var n = names[i];
                    // Throws if .NET is broken...
                    Throw.CheckState( n != null && o != null );
                    d.Add( names[i], o );
                }
                values = d;
                monitor.Info( $"Enum type '{CSharpName}', default value selected is '{defaultValueName} = {defValue:D}'." );
                return new DefaultValueInfo( new FieldDefaultValue( defValue, $"{CSharpName}.{defaultValueName}" ) );
            }

            new Null Nullable => Unsafe.As<Null>( base.Nullable );

            public IPocoType UnderlyingType => _underlyingType;

            public ExternalNameAttribute? ExternalName => _externalName;

            public string ExternalOrCSharpName => _externalName?.Name ?? CSharpName;

            public override string StandardName => ExternalOrCSharpName;

            public string DefaultValueName => _defaultValueName;

            public IReadOnlyDictionary<string, object> Values => _values;

            public override DefaultValueInfo DefaultValueInfo => _defInfo;

            IEnumPocoType IEnumPocoType.Nullable => Nullable;
            IEnumPocoType IEnumPocoType.NonNullable => this;

            INamedPocoType INamedPocoType.Nullable => Nullable;
            INamedPocoType INamedPocoType.NonNullable => this;

            IPocoType.ITypeRef? IPocoType.ITypeRef.NextRef => _nextRef;

            IPocoType IPocoType.ITypeRef.Owner => this;

            IPocoType IPocoType.ITypeRef.Type => _underlyingType;

            int IPocoType.ITypeRef.Index => 0;

        }
    }

}
