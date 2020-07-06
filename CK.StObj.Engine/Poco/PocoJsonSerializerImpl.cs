using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

#nullable enable

namespace CK.Setup
{
    public partial class PocoJsonSerializerImpl : ICodeGenerator
    {
        IPocoSupportResult? _pocoSupport;
        ITypeScope? _pocoDirectory;
        IPocoRootInfo? _pocoInfo;
        ITypeScope? _factory;
        ITypeScope? _pocoClass;

        IPocoSupportResult PocoSupport => _pocoSupport!;

        ITypeScope PocoDirectory => _pocoDirectory!;

        IPocoRootInfo PocoInfo => _pocoInfo!;

        ITypeScope Factory => _factory!;

        ITypeScope PocoClass => _pocoClass!;

        public AutoImplementationResult Implement( IActivityMonitor monitor, ICodeGenerationContext c )
        {
            _pocoSupport = c.Assembly.GetPocoSupportResult();
            _pocoDirectory = c.Assembly.FindOrCreateAutoImplementedClass( monitor, typeof( PocoDirectory ) );

            foreach( var root in _pocoSupport.Roots )
            {
                _pocoInfo = root;
                _factory = c.Assembly.FindOrCreateAutoImplementedClass( monitor, root.PocoFactoryClass );
                foreach( var i in root.Interfaces )
                {
                    var interfaceName = i.PocoInterface.ToCSharpName();
                    var readerName = "PocoJsonSerializer.IFactoryReader<" + interfaceName + ">";

                    _factory.TypeDefinition.BaseTypes.Add( new ExtendedTypeName( readerName ) );
                    _factory.Append( interfaceName ).Append( ' ' ).Append( readerName ).Append( ".Read( ref System.Text.Json.Utf8JsonReader r )" ).NewLine()
                            .Append( " => r.TokenType == System.Text.Json.JsonTokenType.Null ? null : new " )
                            .Append( root.PocoClass.Name ).Append( "( ref r, PocoDirectory );" ).NewLine();

                }
                _factory.Append( "public IPoco ReadTyped( ref System.Text.Json.Utf8JsonReader r ) => new " ).Append( root.PocoClass.Name ).Append( "( ref r, PocoDirectory );" ).NewLine();
                _pocoClass = c.Assembly.FindOrCreateAutoImplementedClass( monitor, root.PocoClass );
                ExtendPocoClass();
            }
            return AutoImplementationResult.Success;
        }

        void ExtendPocoClass()
        {
            // Each Poco class is a IWriter and has a constructor that accepts a Utf8JsonReader.
            PocoClass.TypeDefinition.BaseTypes.Add( new ExtendedTypeName( "CK.Core.PocoJsonSerializer.IWriter" ) );
            PocoClass.Append( "public void Write( System.Text.Json.Utf8JsonWriter w, bool withType )" )
                 .OpenBlock()
                 .Append( "if( withType ) { w.WriteStartArray(); w.WriteStringValue( " ).AppendSourceString( PocoInfo.Name ).Append( "); }" ).NewLine()
                 .Append( "w.WriteStartObject();" ).NewLine();
            var write = PocoClass.CreatePart();
            PocoClass.NewLine()
                 .Append( "w.WriteEndObject();" ).NewLine()
                 .Append( "if( withType ) w.WriteEndArray();" ).NewLine()
                 .CloseBlock();

            PocoClass.Append( "public " ).Append( PocoClass.Name ).Append( "( ref System.Text.Json.Utf8JsonReader r, PocoDirectory_CK d ) : this( d )" )
                 .OpenBlock()
                 .Append( "Read( ref r );" )
                 .CloseBlock();

            // Poco has a Read method but it is not (currently) exposed.
            ITypeScopePart read = GenerateReadBody();

            // Fill the "read" and "write" parts in one pass as well as the "ctor" where
            // auto instantiated properties that have no declared setter are new'ed.

            // Read prefix: starts the loop on the "PropertyName" Json fields.
            // For each property, GenerateWriteForType fills the "write" (this is easy) and
            // returns the a WriteInfo wich contains precomputed stuff to implement the "read" part.
            foreach( var p in PocoInfo.PropertyList )
            {
                write.Append( "w.WritePropertyName( " ).AppendSourceString( p.PropertyName ).Append( " );" ).NewLine();
                var writeInfo = GenerateWriteForType( write, p.PropertyName, p.PropertyType, 0, p );

                read.Append( "case " ).AppendSourceString( p.PropertyName ).Append( " : " )
                    .OpenBlock();
                GenerateAssignation( read, p.PropertyName, writeInfo, FromPocoClass );
                read.Append( "break; " )
                    .CloseBlock();
            }
        }


        enum TypeSpec
        {
            Basic,
            Poco,
            PocoDefiner,
            Set,
            List,
            Object,
            StringMap,
            Map
        }

        class WriteInfo
        {
            public readonly bool IsNullable;
            public readonly Type Type;
            public readonly TypeSpec Spec;
            public readonly IPocoInterfaceInfo? PocoType;
            public readonly WriteInfo? Item1;
            public readonly WriteInfo? Item2;
            public readonly IPocoPropertyInfo? PocoProperty;

            public WriteInfo( bool isNullable, Type t, TypeSpec spec, IPocoInterfaceInfo? pocoType, WriteInfo? item1, WriteInfo? item2, IPocoPropertyInfo? property )
            {
                IsNullable = isNullable;
                Type = t;
                Spec = spec;
                PocoType = pocoType;
                Item1 = item1;
                Item2 = item2;
                PocoProperty = property;
            }
        }

        WriteInfo GenerateWriteForType( ICodeWriter write, string variableName, Type t, int depth, IPocoPropertyInfo? p = null )
        {
            // If its an AutoInstantiated property with no setter, it cannot be null.
            bool isNullable = (p != null && (!p.AutoInstantiated || p.HasDeclaredSetter)) && IsNullable( ref t );
            TypeSpec typeSpec = TypeSpec.Basic;
            IPocoInterfaceInfo? pocoType = null;
            WriteInfo? item1 = null;
            WriteInfo? item2 = null;
            bool unsupportedType = false;
            // Null handling: a prefix does the job.
            if( isNullable )
            {
                write.Append( "if( " ).Append( variableName ).Append( " == null ) w.WriteNullValue();" ).NewLine()
                     .Append( "else " )
                     .OpenBlock();
            }

            // 
            if( t == typeof( object ) )
            {
                typeSpec = TypeSpec.Object;
                write.Append( FromPocoClass ).Append( ".WriteObject( w, " ).Append( variableName ).Append( " );" );
            }
            else if( t == typeof( bool ) )
            {
                write.Append( "w.WriteBooleanValue( " ).Append( variableName ).Append( " );" );
            }
            else if( t == typeof( int )
                     || t == typeof( double )
                     || t == typeof( float )
                     || t == typeof( long )
                     || t == typeof( uint )
                     || t == typeof( byte )
                     || t == typeof( sbyte )
                     || t == typeof( short )
                     || t == typeof( ushort )
                     || t == typeof( ulong )
                     || t == typeof( decimal ) )
            {
                write.Append( "w.WriteNumberValue( " ).Append( variableName ).Append( " );" );
            }
            else if( t == typeof( byte[] ) )
            {
                write.Append( "w.WriteBase64StringValue( " ).Append( variableName ).Append( " );" );
            }
            else if( t == typeof( string )
                     || t == typeof( Guid )
                     || t == typeof( DateTime )
                     || t == typeof( DateTimeOffset ) )
            {
                write.Append( "w.WriteStringValue( " ).Append( variableName ).Append( " );" );
            }
            else if( t.IsEnum )
            {
                var eT = Enum.GetUnderlyingType( t );
                write.Append( "w.WriteNumberValue( (" ).AppendCSharpName( eT ).Append( ')' ).Append( variableName ).Append( " );" );
            }
            else if( t.IsGenericType )
            {
                var iterationVariableName = "v" + depth;
                Type genType = t.GetGenericTypeDefinition();
                bool isList = genType == typeof( IList<> ) || genType == typeof( List<> );
                if( isList || genType == typeof( ISet<> ) || genType == typeof( HashSet<> ) )
                {
                    typeSpec = isList ? TypeSpec.List : TypeSpec.Set;
                    write.Append( "w.WriteStartArray();" ).NewLine()
                         .Append( "foreach( var " ).Append( iterationVariableName ).Append( " in " ).Append( variableName ).Append( " )" ).NewLine()
                         .Append( "{" ).NewLine();

                    item1 = GenerateWriteForType( write, iterationVariableName, t.GetGenericArguments()[0], depth + 1 );

                    write.Append( "}" ).NewLine()
                         .Append( "w.WriteEndArray();" ).NewLine();
                }
                else if( genType == typeof( IDictionary<,> ) || genType == typeof( Dictionary<,> ) )
                {
                    var gArgs = t.GetGenericArguments();
                    var tKey = gArgs[0];
                    var tValue = gArgs[1];
                    if( tKey == typeof( string ) )
                    {
                        typeSpec = TypeSpec.StringMap;
                        write.Append( "w.WriteStartObject();" ).NewLine()
                             .Append( "foreach( var " ).Append( iterationVariableName ).Append( " in " ).Append( variableName ).Append( " )" ).NewLine()
                             .OpenBlock()
                             .Append( "w.WritePropertyName( " ).Append( iterationVariableName ).Append( ".Key );" ).NewLine();
                        item1 = GenerateWriteForType( write, iterationVariableName + ".Value", tValue, depth + 1 );
                        write.CloseBlock()
                             .Append( "w.WriteEndObject();" ).NewLine();
                    }
                    else
                    {
                        typeSpec = TypeSpec.Map;
                        write.Append( "w.WriteStartArray();" ).NewLine()
                             .Append( "foreach( var " ).Append( iterationVariableName ).Append( " in " ).Append( variableName ).Append( " )" ).NewLine()
                             .OpenBlock()
                             .Append( "w.WriteStartArray();" ).NewLine();
                        item1 = GenerateWriteForType( write, iterationVariableName + ".Key", tKey, depth + 1 );
                        item2 = GenerateWriteForType( write, iterationVariableName + ".Value", tValue, depth + 1 );
                        write.Append( "w.WriteEndArray();" ).NewLine()
                             .CloseBlock()
                             .Append( "w.WriteEndArray();" ).NewLine();
                    }
                }
                else
                {
                    unsupportedType = true;
                }
            }
            else
            {
                unsupportedType = true;
            }
            if( unsupportedType )
            {
                if( typeof( IPoco ).IsAssignableFrom( t ) )
                {
                    typeSpec = TypeSpec.Poco;
                    unsupportedType = false;
                    // If its a real Poco type and not a definer, we write its value directly.
                    // When it's only a definer, we write it with its type.
                    pocoType = PocoSupport.Find( t );
                    bool isPocoDefiner = pocoType == null;
                    write.Append( variableName ).Append( ".Write( w, " ).Append( isPocoDefiner ).Append( " );" );
                    if( isPocoDefiner ) typeSpec = TypeSpec.PocoDefiner;
                }
            }
            if( unsupportedType )
            {
                throw new InvalidOperationException( $"Json serialization is not supported for type '{t.ToCSharpName()}'." );
            }
            if( isNullable ) write.CloseBlock();
            return new WriteInfo( isNullable, t, typeSpec, pocoType, item1, item2, p );
        }

        static bool IsNullable( ref Type t )
        {
            bool isNullable = t.IsClass || t.IsInterface;
            if( !isNullable )
            {
                Type? tN = Nullable.GetUnderlyingType( t );
                if( tN != null )
                {
                    t = tN;
                    isNullable = true;
                }
            }
            return isNullable;
        }

    }
}
