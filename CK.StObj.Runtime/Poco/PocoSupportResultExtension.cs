using CK.CodeGen;
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
            if( @this.PocoLike.ByType.ContainsKey( autoType ) )
            {
                writer.Append( "new " ).AppendCSharpName( autoType ).Append( "();" ).NewLine();
                return;
            }
            if( autoType.IsGenericType )
            {
                Type genType = autoType.GetGenericTypeDefinition();
                if( genType == typeof( IList<> ) || genType == typeof( List<> ) )
                {
                    writer.Append( "new List<" ).AppendCSharpName( autoType.GetGenericArguments()[0] ).Append( ">();" ).NewLine();
                    return;
                }
                if( genType == typeof( IDictionary<,> ) || genType == typeof( Dictionary<,> ) )
                {
                    writer.Append( "new Dictionary<" )
                                        .AppendCSharpName( autoType.GetGenericArguments()[0] )
                                        .Append( ',' )
                                        .AppendCSharpName( autoType.GetGenericArguments()[1] )
                                        .Append( ">();" )
                                        .NewLine();
                    return;
                }
                if( genType == typeof( ISet<> ) || genType == typeof( HashSet<> ) )
                {
                    writer.Append( "new HashSet<" ).AppendCSharpName( autoType.GetGenericArguments()[0] ).Append( ">();" ).NewLine();
                    return;
                }
            }
            throw new ArgumentException( $"Invalid type '{autoType.FullName}': readonly properties can only be IPoco (that are not marked with [CKTypeDefiner] or [CKTypeSuperDefiner]), Poco-like objects, ISet<>, Set<>, IList<>, List<>, IDictionary<,> or Dictionary<,>.", nameof( autoType ) );
        }

    }
}
