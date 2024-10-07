using CK.CodeGen;
using CK.Core;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace CK.Setup.PocoJson;

/// <summary>
/// Handles map of <see cref="JsonCodeWriter"/>.
/// </summary>
sealed class JsonCodeWriterMap : Setup.ExportCodeWriterMap
{
    public JsonCodeWriterMap( IPocoTypeNameMap nameMap )
        : base( nameMap )
    {
    }

    protected override ExportCodeWriter CreateAnyWriter() => new AnyWriter( this );

    protected override ExportCodeWriter GetOrCreateWriter( IPocoType t )
    {
        switch( t.Kind )
        {
            case PocoTypeKind.UnionType:
            case PocoTypeKind.AbstractPoco:
            case PocoTypeKind.Any:
                // Mapped to the Any.
                return GetAnyWriter();
            case PocoTypeKind.PrimaryPoco:
                return new PocoWriter( this, Unsafe.As<IPrimaryPocoType>( t ) );
            case PocoTypeKind.SecondaryPoco:
                // Mapped to the PrimaryPoco.
                return GetWriter( Unsafe.As<ISecondaryPocoType>( t ).PrimaryPocoType );
            case PocoTypeKind.Basic:
                return CreateBasicTypeCodeWriter( this, t );
            case PocoTypeKind.Array:
            {
                var type = Unsafe.As<ICollectionPocoType>( t );
                return type.ItemTypes[0].Type == typeof( byte )
                                                ? new BasicWriter( this, ( w, v ) => w.Append( "w.WriteBase64StringValue( " ).Append( v ).Append( " );" ) )
                                                : CreateEnumerableWriter( type );
            }
            case PocoTypeKind.List:
            case PocoTypeKind.HashSet:
                return CreateEnumerableWriter( Unsafe.As<ICollectionPocoType>( t ) );
            case PocoTypeKind.Dictionary:
                return new DictionaryWriter( this, Unsafe.As<ICollectionPocoType>( t ) );
            case PocoTypeKind.Record:
                return new NamedRecordWriter( this, Unsafe.As<IRecordPocoType>( t ) );
            case PocoTypeKind.AnonymousRecord:
                var r = Unsafe.As<IRecordPocoType>( t );
                return r.IsRegular
                        ? new AnonymousRecordWriter( this, r )
                        : GetWriter( r.RegularType );
            case PocoTypeKind.Enum:
            {
                var tE = (IEnumPocoType)t;
                var underlyingWriter = GetWriter( tE.UnderlyingType );
                return new BasicWriter( this, ( writer, v ) => underlyingWriter.GenerateWrite( writer, tE.UnderlyingType, $"(({tE.UnderlyingType.CSharpName}){v})" ) );
            }
            default: throw new NotSupportedException( t.Kind.ToString() );
        }
    }

    static BasicWriter CreateBasicTypeCodeWriter( ExportCodeWriterMap map, IPocoType type )
    {
        if( type.Type == typeof( int )
            || type.Type == typeof( uint )
            || type.Type == typeof( short )
            || type.Type == typeof( ushort )
            || type.Type == typeof( byte )
            || type.Type == typeof( sbyte )
            || type.Type == typeof( double )
            || type.Type == typeof( float ) )
        {
            return new BasicWriter( map, NumberWriter );
        }
        if( type.Type == typeof( string )
                 || type.Type == typeof( Guid )
                 || type.Type == typeof( DateTime )
                 || type.Type == typeof( DateTimeOffset ) )
        {
            return new BasicWriter( map, StringWriter );
        }
        else if( type.Type == typeof( bool ) )
        {
            return new BasicWriter( map, ( writer, v ) => writer.Append( "w.WriteBooleanValue( " ).Append( v ).Append( " );" ) );
        }
        else if( type.Type == typeof( decimal )
                 || type.Type == typeof( long )
                 || type.Type == typeof( ulong ) )
        {
            return new BasicWriter( map, NumberAsStringWriter );
        }
        else if( type.Type == typeof( BigInteger ) )
        {
            // Use the BigInteger.ToString(String) method with the "R" format specifier to generate the string representation of the BigInteger value.
            // Otherwise, the string representation of the BigInteger preserves only the 50 most significant digits of the original value, and data may
            // be lost when you use the Parse method to restore the BigInteger value.
            return new BasicWriter( map, ( writer, v ) => writer.Append( "w.WriteStringValue( " )
                                                                     .Append( v )
                                                                     .Append( ".ToString( \"R\", System.Globalization.NumberFormatInfo.InvariantInfo ) );" ) );
        }
        else if( type.Type == typeof( TimeSpan ) )
        {
            // See here why we don't use ISO 8601 for TimeSpan.
            // https://github.com/dotnet/runtime/issues/28862#issuecomment-1273503317
            return new BasicWriter( map, ( writer, v ) => writer.Append( "w.WriteStringValue( " )
                                                                     .Append( v )
                                                                     .Append( ".Ticks.ToString( System.Globalization.NumberFormatInfo.InvariantInfo ) );" ) );
        }
        else if( type.Type == typeof( SimpleUserMessage )
                 || type.Type == typeof( UserMessage )
                 || type.Type == typeof( FormattedString )
                 || type.Type == typeof( MCString )
                 || type.Type == typeof( CodeString ) )
        {
            return new BasicWriter( map, GlobalizationTypesWriter );
        }
        else if( type.Type == typeof( NormalizedCultureInfo )
                 || type.Type == typeof( ExtendedCultureInfo ) )
        {
            return new BasicWriter( map, CultureInfoWriter );
        }
        return Throw.NotSupportedException<BasicWriter>( type.Type.ToCSharpName() );

        static void NumberWriter( ICodeWriter writer, string variableName )
        {
            writer.Append( "w.WriteNumberValue( " ).Append( variableName ).Append( " );" );
        }

        static void StringWriter( ICodeWriter write, string variableName )
        {
            write.Append( "w.WriteStringValue( " ).Append( variableName ).Append( " );" );
        }

        static void CultureInfoWriter( ICodeWriter write, string variableName )
        {
            write.Append( "w.WriteStringValue( " ).Append( variableName ).Append( ".Name );" );
        }

        static void GlobalizationTypesWriter( ICodeWriter write, string variableName )
        {
            write.Append( "CK.Core.GlobalizationJsonHelper.WriteAsJsonArray( w, " )
                                                            .Append( variableName )
                                                            .Append( " );" );
        }

        static void NumberAsStringWriter( ICodeWriter write, string variableName )
        {
            write.Append( "w.WriteStringValue( " ).Append( variableName ).Append( ".ToString( System.Globalization.NumberFormatInfo.InvariantInfo ) );" );
        }
    }

    ExportCodeWriter CreateEnumerableWriter( ICollectionPocoType c )
    {
        Throw.DebugAssert( c.Kind is PocoTypeKind.Array or PocoTypeKind.List or PocoTypeKind.HashSet );
        var tI = c.ItemTypes[0];
        if( tI.Kind is PocoTypeKind.AbstractPoco )
        {
            Throw.DebugAssert( "HashSet cannot contain IPoco (since IsReadOnlyCompliant is false).", c.Kind is PocoTypeKind.Array or PocoTypeKind.List );
            return tI.IsNullable
                        ? GetWriter( nameof( EnumerableAbstractPocoNullWriter ), c, ( key, c ) => new EnumerableAbstractPocoNullWriter( this ) )
                        : GetWriter( nameof( EnumerableAbstractPocoWriter ), c, ( key, c ) => new EnumerableAbstractPocoWriter( this ) );
        }
        if( tI.Kind is PocoTypeKind.PrimaryPoco or PocoTypeKind.SecondaryPoco )
        {
            Throw.DebugAssert( "HashSet cannot contain IPoco (since IsReadOnlyCompliant is false).", c.Kind is PocoTypeKind.Array or PocoTypeKind.List );
            return tI.IsNullable
                    ? GetWriter( nameof( EnumerablePocoNullWriter ), c, ( key, c ) => new EnumerablePocoNullWriter( this ) )
                    : GetWriter( nameof( EnumerablePocoWriter ), c, ( key, c ) => new EnumerablePocoWriter( this ) );
        }
        if( tI.IsPolymorphic )
        {
            // HashSet can contain polymorhic items like basic reference type (Extended/NormalizedCultureInfo).
            return tI.IsNullable
                    ? GetWriter( nameof( EnumerablePolymorphicNullWriter ), c, ( key, c ) => new EnumerablePolymorphicNullWriter( this ) )
                    : GetWriter( nameof( EnumerablePolymorphicWriter ), c, ( key, c ) => new EnumerablePolymorphicWriter( this ) );
        }
        // HashSet<TItem> is not natively covariant. An adapter is generated for
        // value types, string and other basic reference types.
        //
        // The good news is that the adapters for T (value types, string and other basic reference types) are
        // specialized HashSet<T>: we can map to the Regular type (as we handle only regular anonymous record types). 
        // We want array to use AsSpan(), list to to use CollectionsMarshal.AsSpan and hashset to fall back to the IEnumerable.
        Throw.DebugAssert( "Only abstract readonly collections have no regular and we don't work with them here.", tI.RegularType != null );
        tI = tI.RegularType;
        return c.Kind switch
        {
            PocoTypeKind.Array => GetWriter( $"Array_{tI.Index}", c, ( key, c ) => new ArrayWriter( this, tI ) ),
            PocoTypeKind.List => GetWriter( $"List_{tI.Index}", c, ( key, c ) => new ListWriter( this, tI ) ),
            _ => GetWriter( $"Set_{tI.Index}", c, ( key, c ) => new HashSetWriter( this, tI ) ),
        };
    }

}
