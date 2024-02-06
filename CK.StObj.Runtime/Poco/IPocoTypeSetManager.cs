using System;
using CK.Core;
using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// The <see cref="IPocoTypeSystem.SetManager"/> enables manipulation of always coherent set of types.
    /// This enables easy manipulation of allow and disallow lists for any kind of process or restrictions.
    /// <para>
    /// The root sets exposed here share the same low-level rules that are that any sub ou super set created from them will:
    /// <list type="bullet">
    ///     <item>Never contain a <see cref="IAbstractPocoType"/> without <see cref="IPrimaryPocoType"/> implementations.</item>
    ///     <item>Automatically contain Collections of included types.</item>
    /// </list>
    /// See <see cref="IPocoTypeSet"/> for the rules that are enforced.
    /// </para>
    /// </summary>
    public interface IPocoTypeSetManager
    {
        /// <summary>
        /// Empty root set. Can be used as a generator of any other set by first using <see cref="IPocoTypeSet.Include(IEnumerable{IPocoType}, bool)"/>.
        /// </summary>
        IPocoTypeSet None { get; }

        /// <summary>
        /// Empty set with a low-level rules that guaranties that no type marked with <see cref="NonSerializedAttribute"/> will ever appear.
        /// </summary>
        IPocoTypeSet NoneSerializable { get; }

        /// <summary>
        /// Empty set with a low-level rules that guaranties that no type marked with <see cref="NotExchangeableAttribute"/>
        /// or with <see cref="NonSerializedAttribute"/> will ever appear (to be exchangeable a type must be serializable).
        /// </summary>
        IPocoTypeSet NoneExchangeable { get; }

        /// <summary>
        /// Set of all the exchangeable types. This is a subset of the <see cref="AllSerializable"/>.
        /// </summary>
        IPocoTypeSet AllExchangeable { get; }

        /// <summary>
        /// Set of all the serializable types.
        /// </summary>
        IPocoTypeSet AllSerializable { get; }

        /// <summary>
        /// Root set with all the types (except <see cref="IAbstractPocoType"/> without implementations).
        /// Can be used as a generator of any other set by first using <see cref="IPocoTypeSet.Exclude(IEnumerable{IPocoType})"/>.
        /// </summary>
        IPocoTypeSet All { get; }

        /// <summary>
        /// Advanced method that creates a root set with different low-level rules than the <see cref="All"/>.
        /// </summary>
        /// <param name="allowEmptyRecords">Configures the <see cref="IPocoTypeSet.AllowEmptyRecords"/>.</param>
        /// <param name="allowEmptyPocos">Configures the <see cref="IPocoTypeSet.AllowEmptyPocos"/>.</param>
        /// <param name="autoIncludeCollections">Configures the <see cref="IPocoTypeSet.AutoIncludeCollections"/>.</param>
        /// <param name="lowLevelFilter">Low level filter that guaranties the exclusion of types based on any criteria.</param>
        /// <returns>A new configured set with all the types (except of course the ones that have been excluded by <paramref name="lowLevelFilter"/>.</returns>
        IPocoTypeSet CreateAll( bool allowEmptyRecords, bool allowEmptyPocos, bool autoIncludeCollections, Func<IPocoType, bool> lowLevelFilter );

        /// <summary>
        /// Advanced method that creates a root set with different rules than the default <see cref="None"/>.
        /// </summary>
        /// <param name="allowEmptyRecords">Configures the <see cref="IPocoTypeSet.AllowEmptyRecords"/>.</param>
        /// <param name="allowEmptyPocos">Configures the <see cref="IPocoTypeSet.AllowEmptyPocos"/>.</param>
        /// <param name="autoIncludeCollections">Configures the <see cref="IPocoTypeSet.AutoIncludeCollections"/>.</param>
        /// <param name="lowLevelFilter">Low level filter that guaranties the exclusion of types based on any criteria.</param>
        /// <returns>A new configured empty set.</returns>
        IPocoTypeSet CreateNone( bool allowEmptyRecords, bool allowEmptyPocos, bool autoIncludeCollections, Func<IPocoType, bool> lowLevelFilter );
    }


}
