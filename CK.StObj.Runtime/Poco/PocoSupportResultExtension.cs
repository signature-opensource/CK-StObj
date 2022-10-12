using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;

#nullable enable

namespace CK.Setup
{
    /// <summary>
    /// Extends <see cref="IPocoSupportResult"/> with helpers.
    /// </summary>
    public static class PocoSupportResultExtension
    {
        static readonly Type[] _basicPropertyTypes = new Type[]
        {
            typeof( int ),
            typeof( long ),
            typeof( short ),
            typeof( byte ),
            typeof( string ),
            typeof( bool ),
            typeof( double ),
            typeof( float ),
            typeof( object ),
            typeof( DateTime ),
            typeof( DateTimeOffset ),
            typeof( TimeSpan ),
            typeof( Guid ),
            typeof( decimal ),
            typeof( System.Numerics.BigInteger ),
            typeof( uint ),
            typeof( ulong ),
            typeof( ushort ),
            typeof( sbyte ),
        };

        /// <summary>
        /// Gets whether the given type is a basic type. It is one of these types:
        /// <code>
        ///     int, long, short, byte, string, bool, double, float, object, DateTime, DateTimeOffset, TimeSpan,
        ///     Guid, decimal, System.Numerics.BigInteger, uint, ulong, ushort, sbyte. 
        /// </code>
        /// Note that object is considered a basic type: it is eventually any type that belongs to the Poco types closure.
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static bool IsBasicPropertyType( Type t ) => Array.IndexOf( _basicPropertyTypes, t ) >= 0;

        /// <summary>
        /// Gets the <see cref="PocoTypeKind"/> from a type.
        /// </summary>
        /// <param name="type">The type to test.</param>
        /// <param name="isAbstractCollection">
        /// True if this is a supported abstraction of standard collections. See <see cref="IsAbstractCollectionGenericDefinition(Type)"/>.
        /// </param>
        /// <returns>The kind.</returns>
        public static PocoTypeKind GetPocoTypeKind( in NullableTypeTree type, out bool isAbstractCollection )
        {
            isAbstractCollection = false;
            if( type.Kind.IsTupleType() ) return PocoTypeKind.ValueTuple;
            var t = type.Type;
            if( t == typeof(object) ) return PocoTypeKind.Any;
            if( t.IsEnum ) return PocoTypeKind.Enum;
            if( IsBasicPropertyType( t ) ) return PocoTypeKind.Basic;
            if( type.Kind.IsReferenceType() )
            {
                if( t.IsInterface && typeof( IPoco ).IsAssignableFrom( t ) ) return PocoTypeKind.IPoco;
                if( t.IsGenericType )
                {
                    var tGen = t.GetGenericTypeDefinition();
                    if( tGen == typeof( List<> ) || tGen == typeof( HashSet<> ) || tGen == typeof( Dictionary<,> ) )
                    {
                        return PocoTypeKind.StandardCollection;
                    }
                    if( IsAbstractCollectionGenericDefinition( tGen ) )
                    {
                        isAbstractCollection= true;
                        return PocoTypeKind.StandardCollection;
                    }
                }
            }
            return PocoTypeKind.None;
        }

        /// <summary>
        /// Gets whether this type (that must be a <see cref="Type.IsGenericTypeDefinition"/>)
        /// is a IReadOnlyList&lt;&gt;, IReadOnlySet&lt;&gt; IReadOnlyDictionary&lt;,&gt; IReadOnlyCollection&lt;&gt; or
        /// IEnumerable&lt;&gt;.
        /// </summary>
        /// <param name="genericDefinition">Type definition.</param>
        /// <returns>True if this is an abstraction of the standard collections.</returns>
        public static bool IsAbstractCollectionGenericDefinition( Type genericDefinition )
        {
            Throw.CheckArgument( genericDefinition.IsGenericTypeDefinition );
            return genericDefinition == typeof( IReadOnlyCollection<> )
                    || genericDefinition == typeof( IEnumerable<> )
                    || genericDefinition == typeof( IReadOnlyList<> )
                    || genericDefinition == typeof( IReadOnlySet<> )
                    || genericDefinition == typeof( IReadOnlyDictionary<,> );
        }

        /// <summary>
        /// Generates <paramref name="variableName"/> = "new ..." assignation to the writer (typically in a constructor) for
        /// an automatically instantiated readonly property.
        /// This throws a ArgumentException if the <paramref name="autoType"/> is not a valid one (IPoco, Poco-like,
        /// IList, ISet or IDictionary).
        /// <para>
        /// This method is exposed (by the C.StObj.Runtime) typically for serializers implementations.
        /// </para>
        /// </summary>
        /// <param name="this">Poco support result.</param>
        /// <param name="writer">The code writer.</param>
        /// <param name="variableName">The assigned variable name.</param>
        /// <param name="autoType">The type.</param>
        public static void GenerateAutoInstantiatedNewAssignation( this IPocoSupportResult @this, ICodeWriter writer, string variableName, Type autoType )
        {
            writer.Append( variableName ).Append( " = " );
            if( @this.AllInterfaces.TryGetValue( autoType, out IPocoInterfaceInfo? info ) )
            {
                writer.Append( "new " ).Append( info.Root.PocoClass.FullName! ).Append( "();" ).NewLine();
                return;
            }
            if( autoType.IsGenericType )
            {
                Type genType = autoType.GetGenericTypeDefinition();
                if( genType == typeof( List<> ) )
                {
                    writer.Append( "new List<" ).AppendCSharpName( autoType.GetGenericArguments()[0], true, true, true ).Append( ">();" ).NewLine();
                    return;
                }
                if( genType == typeof( Dictionary<,> ) )
                {
                    writer.Append( "new Dictionary<" )
                                        .AppendCSharpName( autoType.GetGenericArguments()[0], true, true, true )
                                        .Append( ',' )
                                        .AppendCSharpName( autoType.GetGenericArguments()[1], true, true, true )
                                        .Append( ">();" )
                                        .NewLine();
                    return;
                }
                if( genType == typeof( HashSet<> ) )
                {
                    writer.Append( "new HashSet<" ).AppendCSharpName( autoType.GetGenericArguments()[0], true, true, true ).Append( ">();" ).NewLine();
                    return;
                }
            }
            Throw.ArgumentException( $"Invalid type '{autoType.FullName}': readonly properties can only be IPoco (that are not marked with [CKTypeDefiner] or [CKTypeSuperDefiner]), HashSet<>, List<>, or Dictionary<,>.", nameof( autoType ) );
        }

    }
}
