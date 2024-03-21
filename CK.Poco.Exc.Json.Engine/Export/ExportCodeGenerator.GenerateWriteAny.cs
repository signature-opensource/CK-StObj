//using CK.CodeGen;
//using CK.Core;
//using System.ComponentModel.Design;
//using System.Linq;

//namespace CK.Setup.PocoJson
//{
//    sealed partial class ExportCodeGenerator
//    {
//        void GenerateWriteNonNullableFinalType()
//        {
//            //_exporterType.Append( "static string[] _typeNames = new string?[] {" );
//            //// The array of names must contain all non nullable types.
//            //// Types that are not serializables have a null name.
//            //foreach( var type in _nameMap.TypeSystem.AllNonNullableTypes )
//            //{
//            //    if( _nameMap.TypeSet.Contains( type ) )
//            //    {
//            //        if( type.IsFinalType )
//            //        {
//            //            _exporterType.AppendSourceString( _nameMap.GetName( type ) );
//            //        }
//            //        else if( type.Nullable.IsFinalType )
//            //        {
//            //            Throw.DebugAssert( "This is a reference type.", !type.Type.IsValueType );
//            //            _exporterType.AppendSourceString( _nameMap.GetName( type ) );
//            //        }
//            //        else
//            //        {
//            //            _exporterType.Append( "null" );
//            //        }
//            //    }
//            //    else
//            //    {
//            //        _exporterType.Append( "null" );
//            //    }
//            //    _exporterType.Append( "," );
//            //}
//            //_exporterType.Append( "};" ).NewLine();

//            //_exporterType
//            //    .GeneratedByComment()
//            //    .Append( """
//            //        internal static void WriteNonNullableFinalType( System.Text.Json.Utf8JsonWriter w,
//            //                                                        CK.Poco.Exc.Json.PocoJsonWriteContext wCtx,
//            //                                                        int index,
//            //                                                        object o )
//            //        {
//            //            index = index >> 1;
//            //            if( !wCtx.Options.TypeLess )
//            //            {
//            //                w.WriteStartArray();
//            //                w.WriteStringValue( _typeNames[index] );
//            //            }
//            //            switch( index )
//            //            {
//            //        """ );
//            //var types = _nameMap.TypeSet.NonNullableTypes.Where( t => t.IsFinalType );
//            //foreach( var t in types )
//            //{
//            //    _exporterType.Append( "case " ).Append( t.Index >> 1 ).Append( ":" )
//            //        .OpenBlock();
//            //    string variableName = $"(({t.ImplTypeName})o)";
//            //    if( t is IRecordPocoType )
//            //    {
//            //        _exporterType.Append( "var vLoc = " ).Append( variableName ).Append( ";" ).NewLine();
//            //        variableName = "vLoc";
//            //    }
//            //    _writerMap.RawWrite( t, _exporterType, variableName );
//            //    _exporterType.NewLine()
//            //        .Append( "break;" )
//            //        .CloseBlock();
//            //}
//            //_exporterType.Append( """
//            //            }
//            //            if( !wCtx.Options.TypeLess ) w.WriteEndArray();
//            //        }

//            //        """ );
//        }

//        // Step 3: Generating the WriteAny that routes any object to its registered Oblivious type.
//        //         This is basically a big switch case on the object.GetType() except that it is broken
//        //         into smaller pieces for readability (and hopefully better performance if there are a lot of types).
//        void GenerateWriteAny()
//        {
//            _exporterType
//                .GeneratedByComment()
//                .Append( """
//                    internal static void WriteAny( System.Text.Json.Utf8JsonWriter w, object o, CK.Poco.Exc.Json.PocoJsonWriteContext wCtx )
//                    {
//                        int index = PocoDirectory_CK.NonNullableFinalTypes.GetValueOrDefault( o.GetType(), -1 );
//                        if( index < 0 ) w.ThrowJsonException( $"Non serializable type: {o.GetType().ToCSharpName(false)}" );
//                        if( wCtx.RuntimeFilter.Contains( index >> 1 ) )
//                        {
//                            WriteNonNullableFinalType( w, wCtx, index, o );
//                        }
//                    }
//                    """ );

//        }

//    }
//}
