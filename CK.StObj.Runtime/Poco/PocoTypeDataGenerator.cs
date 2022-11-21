using CK.Core;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Abstract base class to derive <typeparamref name="T"/> data for each <see cref="IPocoType"/> in a
    /// <see cref="IPocoTypeSystem"/>. This "Template method" pattern encapsulates a <see cref="PocoTypeVisitor"/>.
    /// </summary>
    /// <typeparam name="T">The data type that must be associated to a type.</typeparam>
    public abstract class PocoTypeDataGenerator<T>
    {
        readonly Visitor _visitor;
        T[]? _result;
        [AllowNull]
        IPocoTypeSystem _typeSystem;
        bool _skipNullables;

        sealed class Visitor : PocoTypeVisitor, IReadOnlyDictionary<IPocoType,T>
        {
            readonly PocoTypeDataGenerator<T> _p;

            public Visitor( PocoTypeDataGenerator<T> p )
            {
                _p = p;
            }

            protected override bool Visit( IActivityMonitor monitor, IPocoType t )
            {
                if( _p._skipNullables && t.IsNullable ) return false;
                return base.Visit( monitor, t );
            }

            protected override void VisitAbstractPoco( IActivityMonitor monitor, IAbstractPocoType abstractPoco )
            {
                Debug.Assert( abstractPoco.PrimaryPocoTypes.All( x => LastVisited.Contains( x ) ), "Primaries are registered before Abstracts." );
                Debug.Assert( abstractPoco.OtherAbstractTypes.All( x => LastVisited.Contains( x ) ), "Compatible Abstracts are registered before." );
                _p.SetResult( abstractPoco, _p.OnAbstractPoco( monitor, abstractPoco, this ) );
            }

            protected override void VisitCollection( IActivityMonitor monitor, ICollectionPocoType collection )
            {
                base.Visit( monitor, collection );
                _p.SetResult( collection, _p.OnCollection( monitor, collection, this ) );
            }

            protected override void VisitPrimaryPoco( IActivityMonitor monitor, IPrimaryPocoType primary )
            {
                base.VisitPrimaryPoco( monitor, primary );
                _p.SetResult( primary, _p.OnPrimaryPoco( monitor, primary, this ) );
            }

            protected override void VisitRecord( IActivityMonitor monitor, IRecordPocoType record )
            {
                base.VisitRecord( monitor, record );
                _p.SetResult( record, _p.OnRecord( monitor, record, this ) );
            }

            protected override void VisitUnion( IActivityMonitor monitor, IUnionPocoType union )
            {
                base.VisitUnion( monitor, union );
                _p.SetResult( union, _p.OnUnionType( monitor, union, this ) );
            }

            protected override void VisitBasic( IActivityMonitor monitor, IPocoType basic )
            {
                _p.SetResult( basic, _p.OnBasic( monitor, basic, this ) );
            }

            IEnumerable<IPocoType> IReadOnlyDictionary<IPocoType, T>.Keys => LastVisited;

            IEnumerable<T> IReadOnlyDictionary<IPocoType, T>.Values => LastVisited.Select( k => _p.GetResult( k ) );

            int IReadOnlyCollection<KeyValuePair<IPocoType, T>>.Count => LastVisited.Count;

            T IReadOnlyDictionary<IPocoType, T>.this[IPocoType key] => _p.GetResult( key );

            bool IReadOnlyDictionary<IPocoType, T>.ContainsKey( IPocoType key ) => LastVisited.Contains( key );

            bool IReadOnlyDictionary<IPocoType, T>.TryGetValue( IPocoType key, out T value )
            {
                if( LastVisited.Contains( key ) )
                {
                    value = _p.GetResult( key );
                    return true;
                }
                value = default!;
                return false;
            }

            public IEnumerator<KeyValuePair<IPocoType, T>> GetEnumerator()
            {
                return LastVisited.Select( k => KeyValuePair.Create( k,_p.GetResult( k ) ) ).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        void SetResult( IPocoType t, in T r )
        {
            int idx = t.Index;
            if( _skipNullables ) idx >>= 1;
            _result![idx] = r;
        }

        T GetResult( IPocoType t )
        {
            int idx = t.Index;
            if( _skipNullables ) idx >>= 1;
            return _result![idx];
        }

        /// <summary>
        /// Initializes a generator.
        /// <para>
        /// </para>
        /// Using a false <paramref name="skipNullables"/> is possible
        /// but not recommended: usually the nullable information is easy enough to derive from the non
        /// nullable one, allocating such information is often useless.
        /// </summary>
        /// <param name="skipNullables">False to process all the types (and generates an array twice bigger).</param>
        public PocoTypeDataGenerator( bool skipNullables = true )
        {
            _visitor = new Visitor( this );
            _skipNullables = skipNullables;
        }

        /// <summary>
        /// Gets the type system that is being processed.
        /// </summary>
        protected IPocoTypeSystem TypeSystem => _typeSystem;

        /// <summary>
        /// Generates an array of <typeparamref name="T"/> for types.
        /// <para>
        /// This can be called multiple times.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="typeSystem">The type system to process.</param>
        /// <returns>The resulting array.</returns>
        public virtual T[] Generate( IActivityMonitor monitor, IPocoTypeSystem typeSystem )
        {
            _typeSystem = typeSystem;
            var all = _skipNullables ? typeSystem.AllTypes : typeSystem.AllNonNullableTypes;
            _result = new T[all.Count];
            foreach( var t in typeSystem.AllNonNullableTypes )
            {
                _visitor.VisitRoot( monitor, t, t.Index == 0 );
            }
            return _result;
        }

        /// <summary>
        /// Generates the data associated to a <see cref="IUnionPocoType"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="union">The type to process.</param>
        /// <param name="currentData">The already processed data.</param>
        /// <returns>Must return the data to associate with the processed type.</returns>
        protected abstract T OnUnionType( IActivityMonitor monitor, IUnionPocoType union, IReadOnlyDictionary<IPocoType, T> currentData );

        /// <summary>
        /// Generates the data associated to a <see cref="IRecordPocoType"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="record">The type to process.</param>
        /// <param name="currentData">The already processed data.</param>
        /// <returns>Must return the data to associate with the processed type.</returns>
        protected abstract T OnRecord( IActivityMonitor monitor, IRecordPocoType record, IReadOnlyDictionary<IPocoType, T> currentData );

        /// <summary>
        /// Generates the data associated to a <see cref="IPrimaryPocoType"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="primary">The type to process.</param>
        /// <param name="currentData">The already processed data.</param>
        /// <returns>Must return the data to associate with the processed type.</returns>
        protected abstract T OnPrimaryPoco( IActivityMonitor monitor, IPrimaryPocoType primary, IReadOnlyDictionary<IPocoType, T> currentData );

        /// <summary>
        /// Generates the data associated to a <see cref="ICollectionPocoType"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="collection">The type to process.</param>
        /// <param name="currentData">The already processed data.</param>
        /// <returns>Must return the data to associate with the processed type.</returns>
        protected abstract T OnCollection( IActivityMonitor monitor, ICollectionPocoType collection, IReadOnlyDictionary<IPocoType, T> currentData );

        /// <summary>
        /// Generates the data associated to a <see cref="IAbstractPocoType"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="abstractPoco">The type to process.</param>
        /// <param name="currentData">The already processed data.</param>
        /// <returns>Must return the data to associate with the processed type.</returns>
        protected abstract T OnAbstractPoco( IActivityMonitor monitor, IAbstractPocoType abstractPoco, IReadOnlyDictionary<IPocoType, T> currentData );

        /// <summary>
        /// Generates the data associated to a basic type.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="basic">The type to process.</param>
        /// <param name="currentData">The already processed data.</param>
        /// <returns>Must return the data to associate with the processed type.</returns>
        protected abstract T OnBasic( IActivityMonitor monitor, IPocoType basic, IReadOnlyDictionary<IPocoType, T> currentData );
    }
}
