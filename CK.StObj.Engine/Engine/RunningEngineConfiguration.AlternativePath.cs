using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;

namespace CK.Setup
{
    public sealed partial class RunningEngineConfiguration
    {
        /// <summary>
        /// Analyses all [X|Y...] alternatives inside <see cref="NormalizedPath.Parts"/>.
        /// Note that brackets without | inside are ignored: only patterns with at least one | in brackets are
        /// considered.
        /// </summary>
        public readonly struct AlternativePath : IReadOnlyList<string>
        {
            static readonly Regex _regex = new Regex( @"\[(?<1>[^/\]]+)(\|(?<1>[^/\]]+))+\]", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture );
            readonly IReadOnlyList<AlternativeSlot>? _slots;
            readonly NormalizedPath _originPath;
            readonly NormalizedPath _path;

            /// <summary>
            /// Models a [sl|ot].
            /// </summary>
            public readonly struct AlternativeSlot
            {
                internal AlternativeSlot( int pos, int length, IReadOnlyList<string> alternatives )
                {
                    Index = pos;
                    Length = length;
                    Alternatives = alternatives;
                }

                /// <summary>
                /// Gets the index in the <see cref="NormalizedPath.Path"/> of the start of the
                /// open bracket of the [sl|ot].
                /// </summary>
                public int Index { get; }

                /// <summary>
                /// Gets the length of the [sl|ot].
                /// </summary>
                public int Length { get; }

                /// <summary>
                /// Gets the different alternatives in the slot.
                /// </summary>
                public IReadOnlyList<string> Alternatives { get; }
            }

            /// <summary>
            /// Initializes a new <see cref="AlternativePath"/>.
            /// </summary>
            /// <param name="path">The initial BinPath path.</param>
            public AlternativePath( NormalizedPath path )
            {
                Throw.DebugAssert( path.IsRooted );
                _originPath = path;
                _path = path;
                Match m = _regex.Match( path.Path );
                if( m.Success )
                {
                    int count = 1;
                    var slots = new List<AlternativeSlot>();
                    do
                    {
                        // Sort the options.
                        var a = m.Groups[1].Captures.Cast<Capture>().Select( c => c.Value ).ToArray();
                        Array.Sort( a );
                        slots.Add( new AlternativeSlot( m.Index, m.Length, a ) );
                        count *= a.Length;
                    }
                    while( (m = m.NextMatch()).Success );
                    _slots = slots;
                    Count = count;
                }
                else
                {
                    _slots = null;
                    Count = 1;
                }
            }

            /// <summary>
            /// Gets the initial path value.
            /// </summary>
            public NormalizedPath OrginPath => _originPath;

            /// <summary>
            /// Gets the path for which alternatives have been analysed.
            /// </summary>
            public NormalizedPath Path => _path;

            /// <summary>
            /// Gets whether this struct is valid or is the default one.
            /// </summary>
            public bool IsNotDefault => _slots != null;

            /// <summary>
            /// Gets the variable slots possibilites.
            /// </summary>
            public IReadOnlyList<AlternativeSlot> AlternativeSlots => _slots ?? Array.Empty<AlternativeSlot>();

            /// <summary>
            /// Gets the total number of combinations.
            /// </summary>
            public int Count { get; }

            /// <summary>
            /// Gets one of the possible path.
            /// </summary>
            /// <param name="i">The possible path from 0 to <see cref="Count"/> (excluded).</param>
            /// <returns>The path.</returns>
            public string this[int i]
            {
                get
                {
                    Throw.CheckOutOfRangeArgument( i >= 0 && i < Count );
                    StringBuilder b = new StringBuilder();
                    int idxP = 0;
                    for( int iSlot = 0; iSlot < AlternativeSlots.Count; ++iSlot )
                    {
                        var a = AlternativeSlots[iSlot];
                        var c = a.Alternatives.Count;
                        b.Append( _path.Path, idxP, a.Index - idxP )
                                 .Append( a.Alternatives[i % c] );
                        idxP = a.Index + a.Length;
                        i /= c;
                    }
                    b.Append( _path.Path, idxP, _path.Path.Length - idxP );
                    return b.ToString();
                }
            }

            /// <summary>
            /// Gets one of the possible choice among the different <see cref="AlternativeSlots"/>.
            /// </summary>
            /// <param name="i">The possible choice from 0 to <see cref="Count"/> (excluded).</param>
            /// <returns>The path.</returns>
            public string[] Choose( int i )
            {
                Throw.CheckOutOfRangeArgument( i >= 0 && i < Count );
                var r = new string[AlternativeSlots.Count];
                for( int iSlot = 0; iSlot < AlternativeSlots.Count; ++iSlot )
                {
                    var a = AlternativeSlots[iSlot];
                    var c = a.Alternatives.Count;
                    r[iSlot] = a.Alternatives[i % c];
                    i /= c;
                }
                return r;
            }

            /// <summary>
            /// Checks whether this alternative can be applied to another one:
            /// the other one must contain a subset of our <see cref="AlternativeSlots"/>.
            /// </summary>
            /// <param name="other">The other alternative path.</param>
            /// <returns>True if this one can cover the other.</returns>
            public bool CanCover( in AlternativePath other )
            {
                foreach( var a in other.AlternativeSlots )
                {
                    if( FindSlotIndex( a ) < 0 ) return false;
                }
                return true;
            }

            /// <summary>
            /// Apply the choice from this path to another alternate path.
            /// The other one must contain a subset of our <see cref="AlternativeSlots"/>.
            /// </summary>
            /// <param name="i">The possible choice from 0 to <see cref="Count"/> (excluded).</param>
            /// <param name="other">The other alternative path.</param>
            /// <returns>The resulting path.</returns>
            public string Cover( int i, in AlternativePath other )
            {
                var c = Choose( i );
                StringBuilder b = new StringBuilder();
                int idxP = 0;
                for( int iSlot = 0; iSlot < other.AlternativeSlots.Count; ++iSlot )
                {
                    var a = other.AlternativeSlots[iSlot];
                    int idx = FindSlotIndex( a );
                    Throw.CheckState( nameof( CanCover ), idx >= 0 );
                    b.Append( other._path.Path, idxP, a.Index - idxP )
                     .Append( c[idx] );
                    idxP = a.Index + a.Length;
                }
                b.Append( other._path.Path, idxP, other._path.Path.Length - idxP );
                return b.ToString();
            }

            int FindSlotIndex( AlternativeSlot other ) => AlternativeSlots.IndexOf( a => a.Alternatives.SequenceEqual( other.Alternatives ) );

            /// <summary>
            /// Returns the possible alternatives.
            /// </summary>
            /// <returns></returns>
            public IEnumerator<string> GetEnumerator()
            {
                var capture = this;
                return Enumerable.Range( 0, Count ).Select( i => capture[i] ).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

    }
}
