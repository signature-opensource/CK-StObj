using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CK.Setup;

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
                                                 bool isHashSafe,
                                                 bool isPolymorphic,
                                                 IBasicRefPocoType? baseType )
    {
        Throw.DebugAssert( !type.IsValueType );
        Throw.DebugAssert( type != typeof( object ) );
        Throw.DebugAssert( defaultValue != null );
        return new BasicRefType( s, type, csharpName, defaultValue, isHashSafe, isPolymorphic, baseType );
    }

    internal static IPocoType CreateBasicValue( PocoTypeSystemBuilder s,
                                                Type notNullable,
                                                Type nullable,
                                                string csharpName )
    {
        Throw.DebugAssert( notNullable.IsValueType );
        // A basic value type is always initializable.
        // DateTime use, by default, CK.Core.Util.UtcMinValue: DateTime must be UTC.
        return notNullable == typeof( DateTime )
                 ? new BasicValueTypeWithDefaultValue( s, notNullable, nullable, csharpName, FieldDefaultValue.DateTimeDefault )
                 : new PocoType( s, notNullable, csharpName, PocoTypeKind.Basic, t => new NullValueType( t, nullable ) );
    }

    internal static IPocoType CreateNoDefaultBasicValue( PocoTypeSystemBuilder s,
                                                         Type notNullable,
                                                         Type nullable,
                                                         string csharpName )
    {
        Throw.DebugAssert( notNullable.IsValueType );
        // A basic value type is always initializable.
        // DateTime use, by default, CK.Core.Util.UtcMinValue: DateTime must be UTC.
        return new BasicValueTypeWithoutDefaultValue( s, notNullable, nullable, csharpName );
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

    internal sealed class BasicValueTypeWithoutDefaultValue : PocoType
    {
        public BasicValueTypeWithoutDefaultValue( PocoTypeSystemBuilder s,
                                               Type notNullable,
                                               Type nullable,
                                               string csharpName )
            : base( s, notNullable, csharpName, PocoTypeKind.Basic, t => new NullValueType( t, nullable ) )
        {
        }

        public override DefaultValueInfo DefaultValueInfo => DefaultValueInfo.Disallowed;
    }


    internal sealed class BasicRefType : PocoType, IBasicRefPocoType
    {
        readonly IPocoFieldDefaultValue _def;
        readonly IBasicRefPocoType? _baseType;
        IBasicRefPocoType[] _specializations;
        readonly bool _isHashSafe;
        readonly bool _isPolymorphic;

        sealed class Null : NullReferenceType, IBasicRefPocoType
        {
            public Null( IPocoType notNullable )
                : base( notNullable )
            {
            }

            new IBasicRefPocoType NonNullable => Unsafe.As<IBasicRefPocoType>( base.NonNullable );

            public IBasicRefPocoType? BaseType => NonNullable.BaseType?.Nullable;

            public IEnumerable<IBasicRefPocoType> BaseTypes => NonNullable.BaseTypes.Select( t => t.Nullable );

            public IEnumerable<IBasicRefPocoType> Specializations => NonNullable.Specializations.Select( t => t.Nullable );

            public override IBasicRefPocoType ObliviousType => this;

            IBasicRefPocoType IBasicRefPocoType.NonNullable => NonNullable;

            IBasicRefPocoType IBasicRefPocoType.Nullable => this;
        }

        public BasicRefType( PocoTypeSystemBuilder s,
                             Type notNullable,
                             string csharpName,
                             IPocoFieldDefaultValue defaultValue,
                             bool isHashSafe,
                             bool isPolymorphic,
                             IBasicRefPocoType? baseType )
            : base( s, notNullable, csharpName, PocoTypeKind.Basic, static t => new Null( t ) )
        {
            _def = defaultValue;
            _isHashSafe = isHashSafe;
            _isPolymorphic = isPolymorphic;
            _baseType = baseType;
            _specializations = Array.Empty<IBasicRefPocoType>();
            if( baseType != null )
            {
                ((BasicRefType)baseType).AddSpecialization( this );
            }
        }

        new IBasicRefPocoType Nullable => Unsafe.As<IBasicRefPocoType>( base.Nullable );

        public override DefaultValueInfo DefaultValueInfo => new DefaultValueInfo( _def );

        public IBasicRefPocoType? BaseType => _baseType;

        public IEnumerable<IBasicRefPocoType> BaseTypes
        {
            get
            {
                var t = _baseType;
                while( t != null )
                {
                    yield return t;
                    t = t.BaseType;
                }
            }
        }

        public override bool IsReadOnlyCompliant => _isHashSafe;

        public IEnumerable<IBasicRefPocoType> Specializations => _specializations;

        void AddSpecialization( BasicRefType s )
        {
            if( _specializations.Length == 0 )
            {
                _specializations = new IBasicRefPocoType[] { s };
            }
            else
            {
                var a = new IBasicRefPocoType[_specializations.Length + 1];
                Array.Copy( _specializations, 0, a, 0, _specializations.Length );
                a[_specializations.Length] = s;
                _specializations = a;
            }
        }

        public override IBasicRefPocoType ObliviousType => Nullable;

        /// <summary>
        /// This should be "_specializations.Length > 0" but whether a type is polymorphic or not
        /// has a strong impact on serialization. Since we don't have versioned serialization available,
        /// we currently consider that polymorphism is intrinsic regardless of the registered types.
        /// <para>
        /// If, one day, a more aggressive approach is possible, this would require to also handle this
        /// for Abstract Poco: an AbstractPoco that has a single implementation would not be polymorphic.
        /// But this would have to be computed in the context of a PocoTypeSet: one could have the same
        /// PocoType be polymorphic in one set and non polymorphic in another one.
        /// </para>
        /// </summary>
        public override bool IsPolymorphic => _isPolymorphic;

        public override IPocoType? StructuralFinalType => Type.IsAbstract ? null : Nullable;

        IBasicRefPocoType IBasicRefPocoType.Nullable => Nullable;

        IBasicRefPocoType IBasicRefPocoType.NonNullable => this;
    }

}
