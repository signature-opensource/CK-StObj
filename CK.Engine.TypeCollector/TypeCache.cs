using CK.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace CK.Engine.TypeCollector
{

    public sealed partial class TypeCache
    {
        readonly Dictionary<Type, CachedType> _types;
        readonly IAssemblyCache _assemblies;

        public TypeCache( IAssemblyCache assemblies )
        {
            _types = new Dictionary<Type, CachedType>();
            _assemblies = assemblies;
        }

        public ICachedType Get( Type type ) => Get( type, _assemblies.FindOrCreate( type.Assembly ) );

        internal ICachedType Get( Type type, CachedAssembly? knwonAssembly )
        {
            Throw.CheckArgument( type is not null
                                 && type.IsByRef is false );
            if( !_types.TryGetValue( type, out CachedType? c ) )
            {
                // First we must handle Nullable value types.
                Type? nullableValueType = null;
                var isValueType = type.IsValueType;
                if( isValueType )
                {
                    var tNotNull = Nullable.GetUnderlyingType( type );
                    if( tNotNull != null )
                    {
                        nullableValueType = type;
                        type = tNotNull;
                    }
                    else
                    {
                        nullableValueType = typeof(Nullable<>).MakeGenericType( type );
                    }
                }
                // Only then can we work on the type.
                ICachedType? genericTypeDefinition = type.IsGenericType
                                                        ? Get( type.GetGenericTypeDefinition() )
                                                        : null;
                int maxDepth = 0;
                var interfaces = type.GetInterfaces()
                                     .Where( i => i.IsVisible )
                                     .Select( i =>
                                     {
                                         var b = Get( i );
                                         if( maxDepth < b.TypeDepth ) maxDepth = b.TypeDepth;
                                         return b;
                                     } )   
                                     .ToImmutableArray();

                var tBase = type.BaseType;
                ICachedType? baseType = null;
                if( tBase != null && tBase != typeof( object ) )
                {
                    var b = Get( tBase );
                    if( maxDepth < b.TypeDepth ) maxDepth = b.TypeDepth;
                    baseType = b;
                }

                knwonAssembly ??= _assemblies.FindOrCreate( type.Assembly );
                c = new CachedType( this, type, maxDepth+1, nullableValueType, knwonAssembly, interfaces, baseType, genericTypeDefinition );                
            }
            return c;
        }
    }
}
