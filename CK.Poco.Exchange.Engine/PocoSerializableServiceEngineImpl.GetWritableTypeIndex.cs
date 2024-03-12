//using System;
//using System.Linq;
//using CK.CodeGen;
//using CK.Core;

//namespace CK.Setup
//{
//    public sealed partial class PocoSerializableServiceEngineImpl
//    {
//        /// <summary>
//        /// PocoDirectory_CK static helper that determines the IPocoType.Index of an untyped (but declared as serializable) object.
//        /// This can be used by serialization code generator to handle <see cref="IPocoType.IsPolymorphic"/> type: the index
//        /// should then be used (divided by 2, or shifted by &gt;&gt;1) to check a <see cref="ExchangeableRuntimeFilter"/> and
//        /// then to locate a function that generates the write code.
//        /// <para>
//        /// Before calling this, the generated code must ensure that the object to write is not null: this ensures that the object's
//        /// type cannot be a nullable value type (boxing lifts the non nullable type: see here
//        /// https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/nullable-value-types#boxing-and-unboxing).
//        /// </para>
//        /// <list type="bullet">
//        ///   <item>The index is even: the type is not nullable because the object must not be null.</item>
//        ///   <item>A necessary oblivious type index is returned because only oblivious types eventually exists in the C# type system.</item>
//        ///   <item>A necessary non polymorpic type index is returned because Abstract Poco and Union Types don't exist and the empty object
//        ///   is not a valid instance in the Poco world.</item>
//        /// </list>
//        /// <para>
//        /// This is currently a big switch case on the object.GetType() except that it is broken
//        /// into smaller pieces for readability (and hopefully better performance if there are a lot of types).
//        /// It may be replaced with a dictionary at the PocoDirectory level of I(Runtime)PocoType one day that will expose the Index. 
//        /// </para>
//        /// <para>
//        /// The GetWritableTypeIndexAndName function returns (-1,null) if the object is null or if it is not a serializable type.
//        /// </para>
//        /// </summary>
//        /// <param name="pocoDirectory">The poco directory type scope.</param>
//        /// <param name="nameMap">The type name map.</param>
//        static void GenerateGetWritableTypeIndexAndName( ITypeScope pocoDirectory, PocoTypeNameMap nameMap )
//        {
//            pocoDirectory.GeneratedByComment( "This returns (-1,null) if the object is null or if it is not a serializable type." )
//                         .Append( @"
//internal static (int Index, string? TypeName) GetWritableTypeIndexAndName( object o )
//{
//    if( o != null )
//    {
//        var t = o.GetType();
//        if( t.IsValueType )
//        {
//            if( t.IsEnum )
//            {
//                switch( o )
//                {
//                    " ).CreatePart( out var enumCases ).Append( @"
//                }
//            }
//            else if( t.Name.StartsWith( ""ValueTuple`"", StringComparison.Ordinal ) && t.Namespace == ""System"" )
//            {
//                switch( o )
//                {
//                    " ).CreatePart( out var valueTupleCases ).Append( @"
//                }
//            }
//            else switch( o )
//            {
//                " ).CreatePart( out var basicValueTypeCases ).Append( @"
//                " ).CreatePart( out var namedRecordCases ).Append( @"
//            }
//        }
//        else
//        {
//            switch( o )
//            {
//                " ).CreatePart( out var basicRefTypeCases ).Append( @"
//                case IPoco:
//                {
//                    switch( o )
//                    {
//                        " ).CreatePart( out var pocoCases ).Append( @"
//                    }
//                    break;
//                }
//                case Array:
//                {
//                    switch( o )
//                    {
//                        " ).CreatePart( out var arrayCases ).Append( @"
//                    }
//                    break;
//                }
//                " ).CreatePart( out var collectionCases ).Append( @"
//            }
//        }
//    }
//    return (-1,null);
//}" );
//            // Builds the different sorters for cases that must be ordered: arrays and collections
//            // only since these are the only reference types except the basic ones (that 
//            // is currently: the 'string', the Globalization reference types MCString, CodeString, Normalized & ExtendedCultureInfo).
//            var arrays = new ObliviousReferenceTypeSorter();
//            var collections = new ObliviousReferenceTypeSorter();
//            // Among the Globalization reference types, only the Normalized & ExtendedCultureInfo have an issue:
//            // the specialized NormalizedCultureInfo must appear before ExtendedCultureInfo.
//            // We don't use a ObliviousReferenceTypeSorter for 2 types here.
//            IPocoType? extendedCultureInfo = null;
//            IPocoType? normalizedCultureInfo = null;

//            // Non nullable, oblivious and non polymorphic: these are the types that need to be written.
//            foreach( var t in nameMap.TypeSet.NonNullableTypes.Where( t => t.IsOblivious && !t.IsPolymorphic ) )
//            {
//                switch( t.Kind )
//                {
//                    case PocoTypeKind.Basic:
//                        {
//                            if( t.Type.IsValueType )
//                            {
//                                WriteCase( basicValueTypeCases, nameMap, t );
//                            }
//                            else
//                            {
//                                if( t.Type == typeof( string ) )
//                                {
//                                    WriteCase( basicRefTypeCases, nameMap, t );
//                                }
//                                else
//                                {
//                                    if( t.Type == typeof( NormalizedCultureInfo ) )
//                                    {
//                                        normalizedCultureInfo = t;
//                                    }
//                                    else if( t.Type == typeof( ExtendedCultureInfo ) )
//                                    {
//                                        extendedCultureInfo = t;
//                                    }
//                                    else
//                                    {
//                                        Throw.DebugAssert( t.Type == typeof( MCString ) || t.Type == typeof( CodeString ) );
//                                        WriteCase( basicRefTypeCases, nameMap, t );
//                                    }
//                                }
//                            }
//                            break;
//                        }

//                    case PocoTypeKind.Enum:
//                        {
//                            WriteCase( enumCases, nameMap, t );
//                            break;
//                        }

//                    case PocoTypeKind.Array:
//                        {
//                            arrays.Add( t );
//                            break;
//                        }

//                    case PocoTypeKind.PrimaryPoco:
//                        {
//                            WriteCase( pocoCases, nameMap, t );
//                            break;
//                        }
//                    case PocoTypeKind.List:
//                    case PocoTypeKind.HashSet:
//                    case PocoTypeKind.Dictionary:
//                        collections.Add( t );
//                        break;
//                    case PocoTypeKind.AnonymousRecord:
//                        {
//                            // Switch case doesn't work with (tuple, syntax).
//                            WriteCase( valueTupleCases, nameMap, t, t.Type.ToCSharpName( useValueTupleParentheses: false ) );
//                            break;
//                        }
//                    case PocoTypeKind.Record:
//                        {
//                            WriteCase( namedRecordCases, nameMap, t );
//                            break;
//                        }
//                    default:
//                        Throw.NotSupportedException( t.ToString() );
//                        break;
//                }
//            }

//            foreach( var t in arrays.SortedTypes ) WriteCase( arrayCases, nameMap, t );
//            foreach( var t in collections.SortedTypes ) WriteCase( collectionCases, nameMap, t );
//            // Normalized MUST come first.
//            if( normalizedCultureInfo != null ) WriteCase( basicRefTypeCases, nameMap, normalizedCultureInfo );
//            if( extendedCultureInfo != null ) WriteCase( basicRefTypeCases, nameMap, extendedCultureInfo );
//            return;

//            // We keep the pattern with the WriteCase method here: we may reuse (and generalize) this pattern
//            // for untyped object handling one day.
//            static void WriteCase( ITypeScopePart code, PocoTypeNameMap nameMap, IPocoType t, string? typeName = null )
//            {
//                Throw.DebugAssert( t.IsOblivious );
//                code.Append( "case " ).Append( typeName ?? t.ImplTypeName ).Append( " v: return (" )
//                    .Append( t.Index ).Append(",").AppendSourceString( nameMap.GetName(t) ).Append( ");" ).NewLine();
//            }
//        }

//    }

//}
