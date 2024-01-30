using CK.CodeGen;
using CK.Core;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CK.Setup
{
    partial class PocoType
    {
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
                public Null( IPocoType notNullable, Type type )
                    : base( notNullable, type )
                {
                }

                new EnumType NonNullable => Unsafe.As<EnumType>( base.NonNullable );

                public IPocoType UnderlyingType => NonNullable.UnderlyingType.Nullable;

                IEnumPocoType IEnumPocoType.NonNullable => NonNullable;

                IEnumPocoType IEnumPocoType.Nullable => this;

                public ExternalNameAttribute? ExternalName => NonNullable.ExternalName;

                public string ExternalOrCSharpName => NonNullable.ExternalOrCSharpName;

                public string? DefaultValueName => NonNullable.DefaultValueName;
            }

            readonly IPocoType _underlyingType;
            readonly IPocoType.ITypeRef? _nextRef;
            readonly ExternalNameAttribute? _externalName;
            readonly string? _defaultValueName;
            readonly DefaultValueInfo _defInfo;

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
                _externalName = externalName;
                _nextRef = ((PocoType)underlyingType.NonNullable).AddBackRef( this );
                _defInfo = ComputeDefaultValueInfo( monitor, tNotNull, out bool isEmpty, out _defaultValueName );
                if( isEmpty ) SetNotExchangeable( monitor, "Enumeration has no defined values." );
            }

            DefaultValueInfo ComputeDefaultValueInfo( IActivityMonitor monitor, Type tNotNull, out bool isEmpty, out string? defaultValueName )
            {
                // [Doc] The elements of the array are sorted by the binary values (that is, the unsigned values)
                //       of the enumeration constants.
                // 
                // => This is perfect for us: if 0 is defined (that is the "normal" default), then it will be
                //    the first value even if negative exist.
                defaultValueName = null;
                Array values = tNotNull.GetEnumValues();
                if( values.Length == 0 )
                {
                    monitor.Warn( $"Enum type '{CSharpName}' is empty. A valid default value cannot be selected and this enum cannot be exchanged." );
                    isEmpty = true;
                    return DefaultValueInfo.Disallowed;
                }
                isEmpty = false;
                object? value = values.GetValue( 0 );
                if( value == null || (defaultValueName = tNotNull.GetEnumName( value )) == null )
                {
                    monitor.Warn( $"Enum type '{CSharpName}' has a null first value or name for the first value. A valid default value cannot be selected." );
                    return DefaultValueInfo.Disallowed;
                }
                monitor.Info( $"Enum type '{CSharpName}', default value selected is '{defaultValueName} = {value:D}'." );
                return new DefaultValueInfo( new FieldDefaultValue( value, $"{CSharpName}.{defaultValueName}" ) );
            }

            new Null Nullable => Unsafe.As<Null>( base.Nullable );

            public IPocoType UnderlyingType => _underlyingType;

            public ExternalNameAttribute? ExternalName => _externalName;

            public string ExternalOrCSharpName => _externalName != null ? _externalName.Name : CSharpName;

            public override string StandardName => ExternalOrCSharpName;

            public string? DefaultValueName => _defaultValueName;

            public override DefaultValueInfo DefaultValueInfo => _defInfo;

            IEnumPocoType IEnumPocoType.Nullable => Nullable;

            IEnumPocoType IEnumPocoType.NonNullable => this;

            IPocoType.ITypeRef? IPocoType.ITypeRef.NextRef => _nextRef;

            IPocoType IPocoType.ITypeRef.Owner => this;

            IPocoType IPocoType.ITypeRef.Type => _underlyingType;

            int IPocoType.ITypeRef.Index => 0;

        }
    }

}
