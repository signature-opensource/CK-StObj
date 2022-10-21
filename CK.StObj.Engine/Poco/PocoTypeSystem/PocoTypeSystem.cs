using CK.CodeGen;
using CK.Core;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

using NullabilityInfo = System.Reflection.TEMPNullabilityInfo;
using NullabilityInfoContext = System.Reflection.TEMPNullabilityInfoContext;

namespace CK.Setup
{
    /// <summary>
    /// Implementation of <see cref="IPocoTypeSystem"/>.
    /// </summary>
    public sealed partial class PocoTypeSystem : IPocoTypeSystem
    {
        readonly NullabilityInfoContext _nullContext;
        // Indexed by:
        //  - Type: for value types (non nullable type only, basic types and record struct) and IPoco (the interface types).
        //  - CSharpName: for anonymous records (Value Tuples) and reference types (because of nullabilities:
        //                the ? marker does the job).
        readonly Dictionary<object, PocoType> _cache;
        readonly PocoType _objectType;
        readonly PocoType _stringType;
        readonly StringCodeWriter _sharedWriter;
        readonly List<PocoType> _allTypes;
        readonly HalfTypeList _exposedAllTypes;

        /// <summary>
        /// Initializes a new type system with only the basic types registered.
        /// </summary>
        public PocoTypeSystem()
        {
            _nullContext = new NullabilityInfoContext();
            _allTypes = new List<PocoType>( 8192 );
            _exposedAllTypes = new HalfTypeList( _allTypes );
            _cache = new Dictionary<object, PocoType>()
            {
                { typeof(int), PocoType.CreateBasicValue(this, typeof(int), typeof(int?), "int") },
                { typeof(long), PocoType.CreateBasicValue(this, typeof(long), typeof(long?), "long") },
                { typeof(short), PocoType.CreateBasicValue(this, typeof(short), typeof(short?), "short") },
                { typeof(byte), PocoType.CreateBasicValue(this, typeof(byte), typeof(byte?), "byte") },
                { typeof(double), PocoType.CreateBasicValue(this, typeof(double), typeof(double?), "double") },
                { typeof(float), PocoType.CreateBasicValue(this, typeof(float), typeof(float?), "float") },
                { typeof(DateTime), PocoType.CreateBasicValue(this, typeof(DateTime), typeof(DateTime?), "DateTime") },
                { typeof(DateTimeOffset), PocoType.CreateBasicValue(this, typeof(DateTimeOffset), typeof(DateTimeOffset?), "DateTimeOffset") },
                { typeof(TimeSpan), PocoType.CreateBasicValue(this, typeof(TimeSpan), typeof(TimeSpan?), "TimeSpan") },
                { typeof(Guid), PocoType.CreateBasicValue(this, typeof(Guid), typeof(Guid?), "Guid") },
                { typeof(decimal), PocoType.CreateBasicValue(this, typeof(decimal), typeof(decimal?), "decimal") },
                { typeof(System.Numerics.BigInteger), PocoType.CreateBasicValue(this, typeof(System.Numerics.BigInteger), typeof(System.Numerics.BigInteger?), "System.Numerics.BigInteger") },
                { typeof(uint), PocoType.CreateBasicValue(this, typeof(uint), typeof(uint?), "uint") },
                { typeof(ulong), PocoType.CreateBasicValue(this, typeof(ulong), typeof(ulong?), "ulong") },
                { typeof(ushort), PocoType.CreateBasicValue(this, typeof(ushort), typeof(ushort?), "ushort") },
                { typeof(sbyte), PocoType.CreateBasicValue(this, typeof(sbyte), typeof(sbyte?), "sbyte") }
            };
            _cache.Add( "object", _objectType = PocoType.CreateBasicRef( this, typeof( object ), "object", PocoTypeKind.Any ) );
            _cache.Add( "string", _stringType = PocoType.CreateBasicRef( this, typeof( string ), "string", PocoTypeKind.Basic ) );
            _sharedWriter = new StringCodeWriter();
        }

        public IReadOnlyList<IPocoType> AllTypes => _exposedAllTypes;

        public IConcretePocoType? GetConcretePocoType( Type pocoInterface )
        {
            if( _cache.TryGetValue( pocoInterface, out var result ) )
            {
                return result as IConcretePocoType;
            }
            return null;
        }

        public IPrimaryPocoType? GetPrimaryPocoType( Type primaryInterface ) => GetConcretePocoType( primaryInterface ) as IPrimaryPocoType;

        public IPocoType? Register( IActivityMonitor monitor, PropertyInfo p )
        {
            var nullabilityInfo = _nullContext.Create( p );
            return RegisterRoot( monitor, new MemberContext( p ), nullabilityInfo );
        }

        public IPocoType? Register( IActivityMonitor monitor, FieldInfo f )
        {
            var nullabilityInfo = _nullContext.Create( f );
            return RegisterRoot( monitor, new MemberContext( f ), nullabilityInfo );
        }

        public IPocoType? Register( IActivityMonitor monitor, ParameterInfo p )
        {
            var nullabilityInfo = _nullContext.Create( p );
            return RegisterRoot( monitor, new MemberContext( p ), nullabilityInfo );
        }

        IPocoType? RegisterRoot( IActivityMonitor monitor, MemberContext root, NullabilityInfo nullabilityInfo )
        {
            if( nullabilityInfo.Type.IsByRef )
            {
                // Erase the ByRef if any: this why the Type is
                // (unfortunately) given to the called methods that must
                // not used nInfo.Type.
                Type t = nullabilityInfo.Type.GetElementType()!;
                if( !t.IsValueType )
                {
                    monitor.Error( $"Invalid '{root}': ref properties must be used only for record, not for reference type." );
                    return null;
                }
                Debug.Assert( nullabilityInfo.WriteState == NullabilityState.Unknown );
                var result = OnValueType( monitor, t, nullabilityInfo, root );
                if( result == null ) return null;
                if( result.Kind != PocoTypeKind.AnonymousRecord && result.Kind != PocoTypeKind.Record )
                {
                    monitor.Error( $"Invalid '{root}': ref properties must be used only for record. '{result.CSharpName}' is not a Value Tuple nor a record struct." );
                    return null;
                }
                return nullabilityInfo.ReadState == NullabilityState.NotNull ? result : result.Nullable;
            }
            return TryRegister( monitor, nullabilityInfo, root );
        }

        IPocoType? TryRegister( IActivityMonitor monitor,
                                NullabilityInfo nInfo,
                                MemberContext ctx )
        {
            Debug.Assert( !nInfo.Type.IsByRef );
            // First, reject any difference between Read and Write state.
            if( nInfo.WriteState != NullabilityState.Unknown && nInfo.ReadState != nInfo.WriteState )
            {
                monitor.Error( $"Read/Write nullability differ for {ctx}. No [AllowNull], [DisallowNull] or other nullability attributes should be used." );
                return null;
            }
            PocoType? result = nInfo.Type.IsValueType
                                  ? OnValueType( monitor, nInfo.Type, nInfo, ctx )
                                  : OnReferenceType( monitor, nInfo, ctx );
            if( result == null ) return null;
            // Consider Unknown as being nullable (oblivious context).
            bool isNullable = nInfo.ReadState != NullabilityState.NotNull;
            return isNullable ? result.Nullable : result;
        }

        PocoType? OnReferenceType( IActivityMonitor monitor, NullabilityInfo nInfo, MemberContext ctx )
        {
            Type t = nInfo.Type;
            if( t == typeof( object ) ) return _objectType;
            if( t == typeof( string ) ) return _stringType;
            if( t.IsSZArray )
            {
                return OnArray( monitor, t, nInfo, ctx );
            }
            if( t.IsGenericType )
            {
                return OnGenericType( monitor, t, nInfo, ctx );
            }
            if( typeof( IPoco ).IsAssignableFrom( t ) )
            {
                if( !_cache.TryGetValue( t, out var result ) )
                {
                    monitor.Error( $"IPoco '{t}' has been excluded." );
                }
                return result;
            }
            else
            {
                monitor.Error( $"Unsupported Poco type: '{t}'." );
            }
            return null;
        }

        PocoType? OnGenericType( IActivityMonitor monitor, Type t, NullabilityInfo nInfo, MemberContext ctx )
        {
            // Unwrap the nullable value type (or wrap): we reason only on non nullable types.
            var tGen = t.GetGenericTypeDefinition();
            if( tGen == typeof( List<> ) )
            {
                var tI = TryRegister( monitor, nInfo.GenericTypeArguments[0], ctx );
                if( tI == null ) return null;
                var typeName = $"System.Collections.Generic.List<{tI.CSharpName}>";
                if( !_cache.TryGetValue( typeName, out var result ) )
                {
                    result = PocoType.CreateCollection( this, t, typeName, PocoTypeKind.List, tI );
                    _cache.Add( typeName, result );
                }
                return result;
            }
            if( tGen == typeof( HashSet<> ) )
            {
                var tI = TryRegister( monitor, nInfo.GenericTypeArguments[0], ctx );
                if( tI == null ) return null;
                var typeName = $"System.Collections.Generic.HashSet<{tI.CSharpName}>";
                if( !_cache.TryGetValue( typeName, out var result ) )
                {
                    result = PocoType.CreateCollection( this, t, typeName, PocoTypeKind.HashSet, tI );
                    _cache.Add( typeName, result );
                }
                return result;
            }
            if( tGen == typeof( Dictionary<,> ) )
            {
                var tK = TryRegister( monitor, nInfo.GenericTypeArguments[0], ctx );
                if( tK == null ) return null;
                var tV = TryRegister( monitor, nInfo.GenericTypeArguments[1], ctx );
                if( tV == null ) return null;
                var typeName = $"System.Collections.Generic.Dictionary<{tK.CSharpName},{tV.CSharpName}>";
                if( !_cache.TryGetValue( typeName, out var result ) )
                {
                    result = PocoType.CreateCollection( this, t, typeName, PocoTypeKind.Dictionary, tK, tV );
                    _cache.Add( typeName, result );
                }
                return result;
            }
            monitor.Error( $"Unsupported Poco generic type: '{t}'." );
            return null;
        }

        PocoType? OnArray( IActivityMonitor monitor, Type t, NullabilityInfo nInfo, MemberContext ctx )
        {
            Debug.Assert( nInfo.ElementType != null );
            var tE = TryRegister( monitor, nInfo.ElementType, ctx );
            if( tE == null ) return null;
            var typeName = tE.CSharpName + "[]";
            if( !_cache.TryGetValue( typeName, out var result ) )
            {
                result = PocoType.CreateCollection( this, t, typeName, PocoTypeKind.Array, tE );
                _cache.Add( typeName, result );
            }
            return result;
        }

        PocoType? OnValueType( IActivityMonitor monitor, Type t, NullabilityInfo nInfo, MemberContext ctx )
        {
            // Unwrap the nullable value type (or wrap): we reason only on non nullable types.
            Type tNull;
            Type tNotNull;
            if( nInfo.ReadState == NullabilityState.Nullable )
            {
                tNull = t;
                tNotNull = Nullable.GetUnderlyingType( t )!;
                Debug.Assert( tNotNull != null );
                // Unfortunately, the NullabityInfo model is not like ours: there is no GenericTypeArguments
                // for Nullable<>.
                Debug.Assert( nInfo.GenericTypeArguments.Length == 0 && nInfo.ElementType == null );
                // This trick is required...
                nInfo = _nullContext.Create( tNull.GetProperty("Value")! );
            }
            else
            {
                tNotNull = t;
                tNull = typeof( Nullable<> ).MakeGenericType( tNotNull );
            }
            // We must first handle ValueTuple since the cache key must be computed.
            // For other value types, the key is the (non null) type.
            if( tNotNull.IsValueTuple() )
            {
                // Anonymous record: the CSharpName is the key and it can
                // be found in the cache or a new one is created.
                return OnValueTypeAnonymousRecord( monitor, nInfo, ctx, tNull, tNotNull );
            }
            if( !_cache.TryGetValue( tNotNull, out var result ) )
            {
                // No known generic value type is supported.
                if( tNotNull.IsGenericType )
                {
                    monitor.Error( $"Generic value type cannot be a Poco type: {ctx}." );
                }
                else if( tNotNull.IsEnum )
                {
                    // New Enum (basic type).
                    // There is necessary the underlying integral type.
                    result = PocoType.CreateEnum( monitor, this, tNotNull, tNull, _cache[tNotNull.GetEnumUnderlyingType()] );
                    _cache.Add( tNotNull, result );
                }
                else
                {
                    // May be a new "record struct".
                    result = OnValueTypeRecordStruct( monitor, nInfo, ctx, tNull, tNotNull );
                    if( result == null )
                    {
                        monitor.Error( $"Unsupported Poco value type: '{tNotNull}'." );
                    }
                }
            }
            return result;
        }

        PocoType? OnValueTypeAnonymousRecord( IActivityMonitor monitor, NullabilityInfo nInfo, MemberContext ctx, Type tNull, Type tNotNull )
        {
            var subInfos = FlattenValueTuple( nInfo ).ToList();
            var fields = ctx.GetTupleNamedFields( subInfos.Count );
            var b = _sharedWriter.StringBuilder;
            if( b.Length > 0 ) b = new StringBuilder();
            b.Append( '(' );
            int idx = 0;
            foreach( var sub in subInfos )
            {
                var f = fields[idx++];
                IPocoType? type = TryRegister( monitor, sub, ctx );
                if( type == null ) return null;
                if( b.Length != 1 ) b.Append( ',' );
                b.Append( type.CSharpName );
                if( !f.IsUnnamed ) b.Append( ' ' ).Append( f.Name );
                f.SetType( type );
            }
            b.Append( ')' );
            var typeName = b.ToString();
            b.Clear();
            if( !_cache.TryGetValue( typeName, out var exists ) )
            {
                exists = PocoType.CreateRecord( monitor, this, _sharedWriter, tNotNull, tNull, typeName, true, fields );
                _cache.Add( typeName, exists );
            }
            return exists;

            static IEnumerable<NullabilityInfo> FlattenValueTuple( NullabilityInfo nInfo )
            {
                Debug.Assert( nInfo.Type.IsValueTuple() );
                int idx = 0;
                foreach( var info in nInfo.GenericTypeArguments )
                {
                    if( ++idx == 8 )
                    {
                        foreach( var rest in FlattenValueTuple( info ) )
                        {
                            yield return rest;
                        };
                    }
                    yield return info;
                }
            }
        }

        PocoType? OnValueTypeRecordStruct( IActivityMonitor monitor, NullabilityInfo nInfo, MemberContext ctx, Type tNull, Type tNotNull )
        {
            // Record struct are not decorated by any special attribute.
            // Allow only fully mutable struct: all its exposed properties and fields must be mutable.
            monitor.Error( $"Record struct (fully mutable value type) are not yet supported: {ctx}." );
            return null;
        }

    }

}
