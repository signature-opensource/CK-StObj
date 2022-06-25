using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;

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
        /// Gets whether the given type is a basic type that doesn't require a <see cref="IPocoClassInfo"/>.
        /// It is one of these types:
        /// <code>
        ///     int, long, short, byte, string, bool, double, float, object, DateTime, DateTimeOffset, TimeSpan,
        ///     Guid, decimal, System.Numerics.BigInteger, uint, ulong, ushort, sbyte. 
        /// </code>
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static bool IsBasicPropertyType( Type t ) => Array.IndexOf( _basicPropertyTypes, t ) >= 0;

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
            if( @this.PocoClass.ByType.ContainsKey( autoType ) )
            {
                writer.Append( "new " ).AppendCSharpName( autoType, true, true, true ).Append( "();" ).NewLine();
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
            Throw.ArgumentException( $"Invalid type '{autoType.FullName}': readonly properties can only be IPoco (that are not marked with [CKTypeDefiner] or [CKTypeSuperDefiner]), Poco objects (marked with a [PocoClass] attribute), HashSet<>, List<>, or Dictionary<,>.", nameof( autoType ) );
        }

    }
}
