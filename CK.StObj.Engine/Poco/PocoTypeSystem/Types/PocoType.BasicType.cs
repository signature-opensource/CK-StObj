using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CK.Setup
{
    partial class PocoType
    {
        internal static PocoType CreateObject( PocoTypeSystemBuilder s )
        {
            return new PocoType( s, typeof( object ), "object", PocoTypeKind.Any, static t => new NullReferenceType( t ) );
        }

        internal static BasicRefType CreateBasicRef( PocoTypeSystemBuilder s,
                                                     Type type,
                                                     string csharpName,
                                                     FieldDefaultValue defaultValue,
                                                     IBasicRefPocoType? baseType )
        {
            Debug.Assert( !type.IsValueType );
            Debug.Assert( type != typeof( object ) );
            Debug.Assert( defaultValue != null );
            return new BasicRefType( s, type, csharpName, defaultValue, baseType );
        }

        internal static IPocoType CreateBasicValue( PocoTypeSystemBuilder s,
                                                    Type notNullable,
                                                    Type nullable,
                                                    string csharpName )
        {
            Debug.Assert( notNullable.IsValueType );
            // A basic value type is always initializable.
            // DateTime use, by default, CK.Core.Util.UtcMinValue: DateTime must be UTC.
            return notNullable == typeof( DateTime )
                     ? new BasicValueTypeWithDefaultValue( s, notNullable, nullable, csharpName, FieldDefaultValue.DateTimeDefault )
                     : new PocoType( s, notNullable, csharpName, PocoTypeKind.Basic, t => new NullValueType( t, nullable ) );
        }

        /// <summary>
        /// Currently only for DateTime.
        /// </summary>
        internal sealed class BasicValueTypeWithDefaultValue : PocoType
        {
            readonly IPocoFieldDefaultValue _def;

            public BasicValueTypeWithDefaultValue( PocoTypeSystemBuilder s,
                                                   Type notNullable,
                                                   Type nullable,
                                                   string csharpName,
                                                   IPocoFieldDefaultValue defaultValue )
                : base( s, notNullable, csharpName, PocoTypeKind.Basic, t => new NullValueType( t, nullable ) )
            {
                _def = defaultValue;
            }

            public override DefaultValueInfo DefaultValueInfo => new DefaultValueInfo( _def );

        }

        internal sealed class BasicRefType : PocoType, IBasicRefPocoType
        {
            readonly IPocoFieldDefaultValue _def;
            readonly IBasicRefPocoType? _baseType;
            IBasicRefPocoType[] _specializations;

            sealed class Null : NullReferenceType, IBasicRefPocoType
            {
                public Null( IPocoType notNullable )
                    : base( notNullable )
                {
                }

                public IBasicRefPocoType? BaseType => NonNullable.BaseType?.Nullable;

                public IEnumerable<IBasicRefPocoType> BaseTypes => NonNullable.BaseTypes.Select( t => t.Nullable );

                public IEnumerable<IBasicRefPocoType> Specializations => NonNullable.Specializations.Select( t => t.Nullable );

                new IBasicRefPocoType NonNullable => Unsafe.As<IBasicRefPocoType>( base.NonNullable );

                IBasicRefPocoType IBasicRefPocoType.NonNullable => NonNullable;

                IBasicRefPocoType IBasicRefPocoType.Nullable => this;
            }

            public BasicRefType( PocoTypeSystemBuilder s,
                                 Type notNullable,
                                 string csharpName,
                                 IPocoFieldDefaultValue defaultValue,
                                 IBasicRefPocoType? baseType )
                : base( s, notNullable, csharpName, PocoTypeKind.Basic, static t => new NullReferenceType( t ) )
            {
                _def = defaultValue;
                _baseType = baseType;
                _specializations = Array.Empty<IBasicRefPocoType>();
                if( baseType != null )
                {
                    ((BasicRefType)baseType).AddSpecialization( this );
                }
            }

            public override DefaultValueInfo DefaultValueInfo => new DefaultValueInfo( _def );

            public IBasicRefPocoType? BaseType => _baseType;

            public IEnumerable<IBasicRefPocoType> BaseTypes
            {
                get
                {
                    var t = _baseType;
                    while ( t != null )
                    {
                        yield return t;
                        t = t.BaseType;
                    }
                }
            }

            public IEnumerable<IBasicRefPocoType> Specializations => _specializations;

            void AddSpecialization( BasicRefType s )
            {
                if( _specializations.Length == 0 )
                {
                    _specializations = new IBasicRefPocoType[]{ s };
                }
                else
                {
                    var a = new IBasicRefPocoType[_specializations.Length + 1];
                    Array.Copy( _specializations, 0, a, 0, _specializations.Length );
                    a[_specializations.Length] = s;
                    _specializations = a;
                }
            }

            public override bool IsPolymorphic => _specializations.Length > 0;

            public override bool IsNonNullableFinalType => !Type.IsAbstract;

            IBasicRefPocoType IBasicRefPocoType.Nullable => Unsafe.As<IBasicRefPocoType>( base.Nullable );

            IBasicRefPocoType IBasicRefPocoType.NonNullable => this;
        }

    }

}
