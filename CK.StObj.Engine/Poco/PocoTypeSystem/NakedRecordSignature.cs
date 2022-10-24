using System;
using System.Diagnostics;
using System.Text;
using System.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Defines the identity of a list of <see cref="IPocoType"/> regardless
    /// of any type field names.
    /// </summary>
    sealed class NakedRecordSignature : IEquatable<NakedRecordSignature>
    {
        readonly IPocoType[] _types;
        readonly int _hash;
        readonly string? _defSignature;

        NakedRecordSignature( int hash, IPocoType[] types, string? defSignature )
        {
            _hash = hash;
            _types = types;
            _defSignature = defSignature;
        }

        /// <summary>
        /// Creates a naked record signature for a record that may happen to be the naked record itself.
        /// </summary>
        /// <param name="r">The record.</param>
        /// <returns>The signature and whether <paramref name="r"/> happens to be naked.</returns>
        public static (NakedRecordSignature S, bool IsNaked) FromRecord( IRecordPocoType r )
        {
            Debug.Assert( r != null && !r.IsNullable, "We don't work on nullable." );
            var def = r.DefaultValueInfo;
            string? defSignature;
            var types = new IPocoType[r.Fields.Count];
            HashCode hashCode = new HashCode();
            bool isNaked = false;
            if( r.IsAnonymous )
            {
                isNaked = true;
                foreach( var f in r.Fields )
                {
                    hashCode.Add( types[f.Index] = f.Type );
                    isNaked &= f.IsUnnamed;
                }
                defSignature = def.DefaultValue?.ValueCSharpSource;
            }
            else
            {
                defSignature = ComputeCompositeFields( r, types, hashCode );
            }
            hashCode.Add( defSignature );
            return (new NakedRecordSignature( hashCode.ToHashCode(), types, defSignature ), isNaked);
        }

        /// <summary>
        /// Creates a naked record signature for a Poco.
        /// </summary>
        /// <param name="poco">The poco.</param>
        /// <returns>The naked signature.</returns>
        public static NakedRecordSignature FromPoco( IPrimaryPocoType poco )
        {
            HashCode hashCode = new HashCode();
            var types = new IPocoType[poco.Fields.Count];
            var defSignature = ComputeCompositeFields( poco, types, hashCode );
            hashCode.Add( defSignature );
            return new NakedRecordSignature( hashCode.ToHashCode(), types, defSignature );
        }

        static string? ComputeCompositeFields( ICompositePocoType r, IPocoType[] types, HashCode hashCode )
        {
            // We ignore Disallowed here, we consider it as Allowed (no default).
            // A typed record may be Allowed (its default value is the 'default' of the type),
            Debug.Assert( r.DefaultValueInfo.RequiresInit == r.Fields.Any( f => f.DefaultValueInfo.RequiresInit ) );
            if( r.DefaultValueInfo.RequiresInit )
            {
                var b = new StringBuilder( '(' );
                foreach( var f in r.Fields )
                {
                    hashCode.Add( types[f.Index] = f.Type );
                    var fInfo = f.DefaultValueInfo;
                    if( f.Index > 0 ) b.Append( PocoType.Comma );
                    if( fInfo.RequiresInit )
                    {
                        b.Append( fInfo.DefaultValue.ValueCSharpSource );
                    }
                    else
                    {
                        b.Append( "default" );
                    }
                }
                b.Append( ')' );
                return b.ToString();
            }
            foreach( var f in r.Fields )
            {
                hashCode.Add( types[f.Index] = f.Type );
            }
            return null;
        }

        public bool Equals( NakedRecordSignature? other ) => other != null
                                                             && _hash == other._hash
                                                             && _types.SequenceEqual( other._types )
                                                             && _defSignature == other._defSignature;

        public override bool Equals( object? obj ) => Equals( obj as NakedRecordSignature );

        public override int GetHashCode() => _hash;
    }
}
