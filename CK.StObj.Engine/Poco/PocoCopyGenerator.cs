using CK.CodeGen;
using CK.Core;
using System.Collections.Generic;
using System.Linq;

namespace CK.Setup;

/// <summary>
/// Generates the deep copy of the Pocos (<see cref="IPocoGeneratedClass.Clone()"/>) and their creation from any other Poco
/// (<see cref="IPocoFactory.CreateFrom(IPoco)"/>). Everything is generated: no reflection is used at runtime.
/// <para>
/// Each Poco class has:
/// <list type="bullet">
///   <item><c>Clone_CK()</c> that creates a new instance and calls <c>CopyFrom_CK( this )</c>.</item>
///   <item><c>CopyFrom_CK( PocoClass o )</c> that copies all the fields of a Poco of the same type into this new instance.</item>
///   <item>
///   <c>InitFrom_CK( IPoco s )</c> that copies the fields declared by the abstract Pocos that the source implements
///   (or calls <c>CopyFrom_CK</c> when the source is of the same type).
///   </item>
/// </list>
/// The <c>CK.Core.PocoCopier_CK</c> static class contains the helpers for the other types, generated on demand (i is the
/// index of the non nullable type):
/// <list type="bullet">
///   <item><c>C_i( v )</c> deep copies a value (<c>CN_i</c> for a nullable record).</item>
///   <item><c>F_i( c, v )</c> fills the new collection c with the copied items of v.</item>
///   <item><c>X_i( object v )</c> converts a value to the type i (the value of an abstract property to a field).</item>
///   <item><c>XF_i( c, object v )</c> fills the new collection c with the converted items of v.</item>
///   <item><c>CloneAny( object v )</c> deep copies any value thanks to the final types index.</item>
/// </list>
/// </para>
/// <para>
/// The copy is a tree (like a serialization): references shared by the source are not shared in the copy and a cycle
/// is not supported.
/// </para>
/// </summary>
sealed class PocoCopyGenerator
{
    const string _copier = "CK.Core.PocoCopier_CK";

    readonly IActivityMonitor _monitor;
    readonly ICSCodeGenerationContext _context;
    readonly IPocoTypeSystem _typeSystem;
    readonly HashSet<string> _requested;
    readonly Queue<(string Kind, IPocoType Type)> _todo;
    readonly ITypeScope _copierType;

    public PocoCopyGenerator( IActivityMonitor monitor, ICSCodeGenerationContext context, IPocoTypeSystem typeSystem )
    {
        _monitor = monitor;
        _context = context;
        _typeSystem = typeSystem;
        _requested = new HashSet<string>();
        _todo = new Queue<(string, IPocoType)>();
        _copierType = context.Assembly.Code.Global.FindOrCreateNamespace( "CK.Core" )
                                                  .CreateType( "static class PocoCopier_CK" );
    }

    public bool Run()
    {
        using var _ = _monitor.OpenInfo( "Generating the Poco copy support." );
        _copierType.GeneratedByComment().NewLine();
        GenerateCloneAny();
        foreach( var p in _typeSystem.AllTypes.OfType<IPrimaryPocoType>().Where( t => !t.IsNullable ) )
        {
            GeneratePocoClassSupport( p );
        }
        while( _todo.TryDequeue( out var w ) )
        {
            GenerateHelper( w.Kind, w.Type );
        }
        return true;
    }

    void GenerateCloneAny()
    {
        _copierType.Append( "internal static object? CloneAny( object? v )" )
                   .OpenBlock()
                   .Append( "if( v == null ) return null;" ).NewLine()
                   .Append( "switch( CK.Core.PocoDirectory_CK.NonNullableFinalTypes.GetValueOrDefault( v.GetType(), -1 ) )" )
                   .OpenBlock();
        foreach( var t in _typeSystem.NonNullableFinalTypes )
        {
            if( !IsImmutable( t ) )
            {
                _copierType.Append( "case " ).Append( t.Index ).Append( ": return " )
                           .Append( Clone( t, $"(({t.ImplTypeName})v)" ) ).Append( ";" ).NewLine();
            }
        }
        _copierType.Append( "case -1: throw new System.InvalidOperationException( $\"Unable to copy a '{v.GetType()}': it is not a registered Poco compliant type.\" );" ).NewLine()
                   .Append( "default: return v;" )
                   .CloseBlock()
                   .CloseBlock();
    }

    void GeneratePocoClassSupport( IPrimaryPocoType p )
    {
        var tB = _context.Assembly.Code.Global.FindOrCreateAutoImplementedClass( _monitor, p.FamilyInfo.PocoClass );
        string cls = p.ImplTypeName;
        using var region = tB.Region();
        tB.Append( "IPoco IPocoGeneratedClass.Clone() => Clone_CK();" ).NewLine()
          .Append( "internal " ).Append( cls ).Append( " Clone_CK()" )
          .OpenBlock()
          .Append( "var c = new " ).Append( cls ).Append( "();" ).NewLine()
          .Append( "c.CopyFrom_CK( this );" ).NewLine()
          .Append( "return c;" )
          .CloseBlock();

        tB.Append( "internal void CopyFrom_CK( " ).Append( cls ).Append( " o )" )
          .OpenBlock();
        foreach( var f in p.Fields )
        {
            switch( f.FieldAccess )
            {
                case PocoFieldAccessKind.AbstractReadOnly:
                    // The field keeps its default value forever.
                    break;
                case PocoFieldAccessKind.MutableReference:
                    var n = Normalize( f.Type );
                    if( n is IPrimaryPocoType )
                    {
                        tB.Append( "((" ).Append( n.ImplTypeName ).Append( ")" ).Append( f.PrivateFieldName ).Append( ").CopyFrom_CK( (" )
                          .Append( n.ImplTypeName ).Append( ")o." ).Append( f.PrivateFieldName ).Append( " );" ).NewLine();
                    }
                    else
                    {
                        tB.Append( _copier ).Append( "." ).Append( Request( "F", n ) ).Append( "( " )
                          .Append( f.PrivateFieldName ).Append( ", o." ).Append( f.PrivateFieldName ).Append( " );" ).NewLine();
                    }
                    break;
                default:
                    tB.Append( f.PrivateFieldName ).Append( " = " ).Append( Clone( f.Type, "o." + f.PrivateFieldName ) ).Append( ";" ).NewLine();
                    break;
            }
        }
        tB.CloseBlock();

        tB.Append( "internal void InitFrom_CK( IPoco s )" )
          .OpenBlock()
          .Append( "if( s is " ).Append( cls ).Append( " o )" )
          .OpenBlock()
          .Append( "CopyFrom_CK( o );" ).NewLine()
          .Append( "return;" )
          .CloseBlock();
        var abstracts = p.AllAbstractTypes.Select( a => a.NonNullable ).Distinct().ToList();
        foreach( var f in p.Fields )
        {
            if( f.FieldAccess == PocoFieldAccessKind.AbstractReadOnly ) continue;
            // The abstract Pocos that declare the property. When a specialization redeclares it,
            // testing the generalization is enough.
            var decls = abstracts.Where( a => a.Fields.Any( af => af.Name == f.Name ) ).ToList();
            decls.RemoveAll( a => decls.Any( b => b != a && a.AllGeneralizations.Any( g => g.NonNullable == b ) ) );
            if( decls.Count == 0 ) continue;
            // The pattern variables are scoped by the block.
            tB.OpenBlock();
            for( int i = 0; i < decls.Count; ++i )
            {
                if( i > 0 ) tB.Append( "else " );
                tB.Append( "if( s is " ).Append( decls[i].CSharpName ).Append( " a" ).Append( i ).Append( " )" )
                  .OpenBlock()
                  .Append( "object? v = a" ).Append( i ).Append( "." ).Append( f.Name ).Append( ";" ).NewLine();
                GenerateInitField( tB, f );
                tB.CloseBlock();
            }
            tB.CloseBlock();
        }
        tB.CloseBlock();
    }

    void GenerateInitField( ITypeScope tB, IPrimaryPocoField f )
    {
        if( f.FieldAccess == PocoFieldAccessKind.MutableReference )
        {
            var n = Normalize( f.Type );
            tB.Append( "if( v != null ) " );
            if( n is IPrimaryPocoType )
            {
                tB.Append( "((" ).Append( n.ImplTypeName ).Append( ")" ).Append( f.PrivateFieldName ).Append( ").InitFrom_CK( (IPoco)v );" ).NewLine();
            }
            else
            {
                tB.Append( _copier ).Append( "." ).Append( Request( "XF", n ) ).Append( "( " ).Append( f.PrivateFieldName ).Append( ", v );" ).NewLine();
            }
            return;
        }
        if( !f.Type.IsNullable ) tB.Append( "if( v != null ) " );
        // The union type setter checks the allowed types.
        tB.Append( f.Type.Kind == PocoTypeKind.UnionType ? f.Name : f.PrivateFieldName )
          .Append( " = " ).Append( Convert( f.Type, "v" ) ).Append( ";" ).NewLine();
    }

    // The basic types are immutable (even when they are not read-only compliant because they have no value equality,
    // like the UserMessage) and the read-only compliant records only contain immutable values.
    static bool IsImmutable( IPocoType t ) => t.IsReadOnlyCompliant || t.Kind is PocoTypeKind.Basic or PocoTypeKind.Enum;

    static IPocoType Normalize( IPocoType t )
    {
        var n = t.NonNullable;
        return n is ISecondaryPocoType s ? s.PrimaryPocoType : n;
    }

    string Request( string kind, IPocoType n )
    {
        var name = $"{kind}_{n.Index}";
        if( _requested.Add( name ) ) _todo.Enqueue( (kind, n) );
        return name;
    }

    /// <summary>
    /// Gets the expression that deep copies <paramref name="expr"/> of type <paramref name="t"/>.
    /// The expression is evaluated once.
    /// </summary>
    string Clone( IPocoType t, string expr )
    {
        if( IsImmutable( t ) ) return expr;
        if( t.IsPolymorphic ) return $"(({t.ImplTypeName}){_copier}.CloneAny( {expr} ))";
        var n = Normalize( t );
        if( t.IsNullable && t.Type.IsValueType ) return $"{_copier}.{Request( "CN", n )}( {expr} )";
        if( n is IPrimaryPocoType ) return $"{_copier}.{Request( "C", n )}( ({n.ImplTypeName}){expr} )";
        return $"{_copier}.{Request( "C", n )}( {expr} )";
    }

    /// <summary>
    /// Gets the expression that converts <paramref name="expr"/> (an object) to the type <paramref name="t"/>.
    /// The expression is evaluated once. For a non nullable type, expr must not be null.
    /// </summary>
    string Convert( IPocoType t, string expr )
    {
        if( IsImmutable( t ) )
        {
            // The ImplTypeName of a nullable value type is the Nullable<T>.
            return $"(({t.ImplTypeName}){expr})";
        }
        if( t.IsPolymorphic ) return $"(({t.ImplTypeName}){_copier}.CloneAny( {expr} ))";
        var n = Normalize( t );
        if( n is IRecordPocoType )
        {
            return t.IsNullable
                    ? $"{_copier}.{Request( "CN", n )}( ({n.ImplTypeName}?){expr} )"
                    : $"{_copier}.{Request( "C", n )}( ({n.ImplTypeName}){expr} )";
        }
        return $"{_copier}.{Request( "X", n )}( {expr} )";
    }

    void GenerateHelper( string kind, IPocoType n )
    {
        var c = _copierType;
        string tN = n.ImplTypeName;
        switch( kind )
        {
            case "C":
                c.Append( "internal static " ).Append( tN ).Append( " C_" ).Append( n.Index ).Append( "( " ).Append( tN ).Append( " v )" )
                 .OpenBlock();
                switch( n )
                {
                    case IPrimaryPocoType:
                        c.Append( "return v?.Clone_CK();" );
                        break;
                    case IRecordPocoType r:
                        foreach( var f in r.Fields )
                        {
                            if( IsImmutable( f.Type ) ) continue;
                            var access = r.IsAnonymous ? $"v.Item{f.Index + 1}" : $"v.{f.Name}";
                            c.Append( access ).Append( " = " ).Append( Clone( f.Type, access ) ).Append( ";" ).NewLine();
                        }
                        c.Append( "return v;" );
                        break;
                    case ICollectionPocoType a when a.Kind == PocoTypeKind.Array:
                        c.Append( "if( v == null || v.Length == 0 ) return v;" ).NewLine()
                         .Append( "var c = (" ).Append( tN ).Append( ")v.Clone();" ).NewLine();
                        if( !IsImmutable( a.ItemTypes[0] ) )
                        {
                            c.Append( "for( int i = 0; i < c.Length; ++i ) c[i] = " ).Append( Clone( a.ItemTypes[0], "c[i]" ) ).Append( ";" ).NewLine();
                        }
                        c.Append( "return c;" );
                        break;
                    default:
                        c.Append( "if( v == null ) return null;" ).NewLine()
                         .Append( "var c = new " ).Append( tN ).Append( "();" ).NewLine()
                         .Append( Request( "F", n ) ).Append( "( c, v );" ).NewLine()
                         .Append( "return c;" );
                        break;
                }
                c.CloseBlock();
                break;
            case "CN":
                c.Append( "internal static " ).Append( tN ).Append( "? CN_" ).Append( n.Index ).Append( "( " ).Append( tN ).Append( "? v ) => v.HasValue ? " )
                 .Append( Request( "C", n ) ).Append( "( v.Value ) : null;" ).NewLine();
                break;
            case "F":
                {
                    var coll = (ICollectionPocoType)n;
                    c.Append( "internal static void F_" ).Append( n.Index ).Append( "( " ).Append( tN ).Append( " c, " ).Append( tN ).Append( " v )" )
                     .OpenBlock();
                    if( n.Kind == PocoTypeKind.Dictionary )
                    {
                        c.Append( "foreach( var kv in v ) c.Add( kv.Key, " ).Append( Clone( coll.ItemTypes[1], "kv.Value" ) ).Append( " );" );
                    }
                    else
                    {
                        c.Append( "foreach( var e in v ) c.Add( " ).Append( Clone( coll.ItemTypes[0], "e" ) ).Append( " );" );
                    }
                    c.CloseBlock();
                    break;
                }
            case "X":
                c.Append( "internal static " ).Append( tN ).Append( " X_" ).Append( n.Index ).Append( "( object? v )" )
                 .OpenBlock()
                 .Append( "if( v == null ) return null;" ).NewLine();
                if( n is IPrimaryPocoType )
                {
                    c.Append( "var c = new " ).Append( tN ).Append( "();" ).NewLine()
                     .Append( "c.InitFrom_CK( (IPoco)v );" ).NewLine()
                     .Append( "return c;" );
                }
                else
                {
                    var coll = (ICollectionPocoType)n;
                    c.Append( "if( v is " ).Append( tN ).Append( " t ) return " ).Append( Request( "C", n ) ).Append( "( t );" ).NewLine();
                    if( n.Kind == PocoTypeKind.Array )
                    {
                        c.Append( "var l = new System.Collections.Generic.List<" ).Append( coll.ItemTypes[0].CSharpName ).Append( ">();" ).NewLine()
                         .Append( "foreach( var e in (System.Collections.IEnumerable)v ) l.Add( " ).Append( Convert( coll.ItemTypes[0], "e" ) ).Append( " );" ).NewLine()
                         .Append( "return l.ToArray();" );
                    }
                    else
                    {
                        c.Append( "var c = new " ).Append( tN ).Append( "();" ).NewLine()
                         .Append( Request( "XF", n ) ).Append( "( c, v );" ).NewLine()
                         .Append( "return c;" );
                    }
                }
                c.CloseBlock();
                break;
            case "XF":
                {
                    var coll = (ICollectionPocoType)n;
                    c.Append( "internal static void XF_" ).Append( n.Index ).Append( "( " ).Append( tN ).Append( " c, object v )" )
                     .OpenBlock()
                     .Append( "if( v is " ).Append( tN ).Append( " t )" )
                     .OpenBlock()
                     .Append( Request( "F", n ) ).Append( "( c, t );" ).NewLine()
                     .Append( "return;" )
                     .CloseBlock();
                    if( n.Kind == PocoTypeKind.Dictionary )
                    {
                        c.Append( "foreach( System.Collections.DictionaryEntry kv in (System.Collections.IDictionary)v ) c.Add( (" )
                         .Append( coll.ItemTypes[0].ImplTypeName ).Append( ")kv.Key, " ).Append( Convert( coll.ItemTypes[1], "kv.Value" ) ).Append( " );" );
                    }
                    else
                    {
                        c.Append( "foreach( var e in (System.Collections.IEnumerable)v ) c.Add( " ).Append( Convert( coll.ItemTypes[0], "e" ) ).Append( " );" );
                    }
                    c.CloseBlock();
                    break;
                }
            default:
                Throw.NotSupportedException( kind );
                break;
        }
    }
}
