using System.Diagnostics;
using System;

namespace CK.Setup
{
    partial class PocoType
    {
        internal static PocoType CreateBasicRef( PocoTypeSystem s,
                                                 Type type,
                                                 string csharpName,
                                                 PocoTypeKind kind )
        {
            Debug.Assert( !type.IsValueType );
            Debug.Assert( type == typeof( string ) || type == typeof( object ) );
            Debug.Assert( kind == PocoTypeKind.Any || kind == PocoTypeKind.Basic );
            // A string field is allowed (RequiresInit) since, by default, string fields use the FieldDefaultValue.StringDefault.
            return type == typeof( string )
                    ? new BasicTypeWithDefaultValue( s, type, csharpName, kind, FieldDefaultValue.StringDefault, t => new NullReferenceType( t ) )
                    : new PocoType( s, type, csharpName, kind, t => new NullReferenceType( t ) );
        }

        internal static PocoType CreateBasicValue( PocoTypeSystem s,
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

        sealed class BasicTypeWithDefaultValue : PocoType
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
