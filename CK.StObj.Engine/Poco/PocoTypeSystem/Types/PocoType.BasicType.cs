using System.Diagnostics;
using System;
using System.Data.SqlTypes;
using System.Collections.Generic;

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

            public BasicRefType( PocoTypeSystemBuilder s,
                                 Type notNullable,
                                 string csharpName,
                                 IPocoFieldDefaultValue defaultValue,
                                 IBasicRefPocoType? baseType )
                : base( s, notNullable, csharpName, PocoTypeKind.Basic, static t => new NullReferenceType( t ) )
            {
                _def = defaultValue;
                _baseType = baseType;
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

        }

    }

}
