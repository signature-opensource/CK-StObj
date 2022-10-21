using CK.CodeGen;
using CK.Core;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CK.Setup
{
    partial class PocoType
    {
        internal static EnumType CreateEnum( IActivityMonitor monitor, PocoTypeSystem s, Type tNotNull, Type tNull, IPocoType underlyingType )
        {
            Debug.Assert( tNotNull.IsEnum );
            return new EnumType( monitor, s, tNotNull, tNull, underlyingType );
        }

        internal sealed class EnumType : PocoType, IEnumPocoType
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
            }
            DefaultValueInfo _defInfo;

            public EnumType( IActivityMonitor monitor, PocoTypeSystem s, Type tNotNull, Type tNull, IPocoType underlyingType )
                : base( s,
                        tNotNull,
                        tNotNull.ToCSharpName(),
                        PocoTypeKind.Enum,
                        t => new Null( t, tNull ) )
            {
                UnderlyingType = underlyingType;
                _defInfo = ComputeDefaultValueInfo( monitor, tNotNull );
            }

            DefaultValueInfo ComputeDefaultValueInfo( IActivityMonitor monitor, Type tNotNull )
            {
                // An enum has FieldStatus allowed if and only if the 0 is a valid value for the enum.
                if( tNotNull.IsEnumDefined( 0 ) )
                {
                    return DefaultValueInfo.Allowed;
                }
                // If not, we consider the smaller numerical value.
                object? value;
                string? name;
                var values = tNotNull.GetEnumValues().OfType<object>();
                if( !values.Any()
                    || (value = values.Min()) == null
                    || (name = tNotNull.GetEnumName( value )) == null )
                {
                    monitor.Warn( $"Enum type '{CSharpName}' is empty or has a null first value or name for the first value. A valid default value cannot be selected." );
                    return DefaultValueInfo.Disallowed;
                }
                monitor.Warn( $"Enum type '{CSharpName}', default value selected is '{name} = {value}'." );
                return new DefaultValueInfo( new FieldDefaultValue( value, $"{CSharpName}.{name}" ) );
            }

            new Null Nullable => Unsafe.As<Null>( base.Nullable );

            public IPocoType UnderlyingType { get; }

            public override DefaultValueInfo DefaultValueInfo => _defInfo;

            IEnumPocoType IEnumPocoType.Nullable => Nullable;

            IEnumPocoType IEnumPocoType.NonNullable => this;

        }
    }

}
