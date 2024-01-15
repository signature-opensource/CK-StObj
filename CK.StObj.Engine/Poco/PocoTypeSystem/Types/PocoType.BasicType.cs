using System.Diagnostics;
using System;

namespace CK.Setup
{
    partial class PocoType
    {
        internal static PocoType CreateObject( PocoTypeSystem s )
        {
            return new PocoType( s, typeof(object), "object", PocoTypeKind.Any, static t => new NullReferenceType( t ) );
        }

        internal static PocoType CreateBasicRef( PocoTypeSystem s,
                                                 Type type,
                                                 string csharpName,
                                                 FieldDefaultValue defaultValue )
        {
            Debug.Assert( !type.IsValueType );
            Debug.Assert( type != typeof( object ) );
            Debug.Assert( defaultValue != null );
            return new BasicTypeWithDefaultValue( s, type, csharpName, PocoTypeKind.Basic, defaultValue, t => new NullReferenceType( t ) );
        }

        internal static IPocoType CreateBasicValue( PocoTypeSystem s,
                                                    Type notNullable,
                                                    Type nullable,
                                                    string csharpName )
        {
            Debug.Assert( notNullable.IsValueType );
            // A basic value type is always initializable.
            // DateTime use, by default, CK.Core.Util.UtcMinValue: DateTime must be default to UTC.
            return notNullable == typeof( DateTime )
                     ? new BasicTypeWithDefaultValue( s, notNullable, csharpName, PocoTypeKind.Basic, FieldDefaultValue.DateTimeDefault, t => new NullValueType( t, nullable ) )
                     : new PocoType( s, notNullable, csharpName, PocoTypeKind.Basic, t => new NullValueType( t, nullable ) );
        }

        /// <summary>
        /// This is internal because this is used from the PocoTypeSystem constructor for strings,
        /// CK.Globalization types and other 
        /// </summary>
        internal sealed class BasicTypeWithDefaultValue : PocoType
        {
            readonly IPocoFieldDefaultValue _def;

            public BasicTypeWithDefaultValue( PocoTypeSystem s,
                                              Type notNullable,
                                              string csharpName,
                                              PocoTypeKind kind,
                                              IPocoFieldDefaultValue defaultValue,
                                              Func<PocoType, IPocoType> nullFactory )
                : base( s, notNullable, csharpName, kind, nullFactory )
            {
                _def = defaultValue;
            }

            public override DefaultValueInfo DefaultValueInfo => new DefaultValueInfo( _def );

        }

    }

}
