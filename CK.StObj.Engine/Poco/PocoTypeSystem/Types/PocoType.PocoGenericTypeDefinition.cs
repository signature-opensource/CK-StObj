using CK.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Xml.Linq;

namespace CK.Setup
{
    public partial class PocoType
    {
        internal static PocoGenericTypeDefinition CreateGenericTypeDefinition( Type tGen )
        {
            return new PocoGenericTypeDefinition( tGen );
        }

        internal sealed class PocoGenericTypeDefinition : IPocoGenericTypeDefinition
        {
            readonly Type _type;
            readonly List<AbstractPocoType> _instances;
            readonly GenParam[] _parameters;

            sealed class GenParam : IPocoGenericParameter
            {
                readonly string _name;
                internal readonly string _typePropName;
                readonly GenericParameterAttributes _attributes;

                public GenParam( Type t )
                {
                    Throw.DebugAssert( t.IsGenericTypeParameter );
                    _name = t.Name;
                    _typePropName = $"{t.Name}Type";
                    _attributes = t.GenericParameterAttributes;
                }

                public string Name => _name;

                public GenericParameterAttributes Attributes => _attributes;
            }

            public PocoGenericTypeDefinition( Type type )
            {
                _type = type;
                _instances = new List<AbstractPocoType>();
                var parameters = type.GetGenericArguments();
                _parameters = new GenParam[parameters.Length];
                for( int i = 0; i < parameters.Length; i++ )
                {
                    _parameters[i] = new GenParam( parameters[i] );
                }
            }
            public Type Type => _type;

            public IReadOnlyList<IPocoGenericParameter> Parameters => _parameters;

            public IReadOnlyCollection<IAbstractPocoType> Instances => _instances;

            internal void AddInstance( AbstractPocoType t ) => _instances.Add( t );

            internal bool InitializeGenericInstanceArguments( IPocoTypeSystemBuilder typeSystem, IActivityMonitor monitor )
            {
                foreach( var i in _instances )
                {
                    var arguments = CreateArguments( monitor, typeSystem, i.Type );
                    if( arguments == null ) return false;
                    i.SetGenericArguments( arguments );
                }
                return true;
            }

            internal (IPocoGenericParameter Parameter, IPocoType Type)[]? CreateArguments( IActivityMonitor monitor,
                                                                                           IPocoTypeSystemBuilder typeSystem,
                                                                                           Type instanceType )
            {
                Throw.DebugAssert( instanceType.IsInterface && instanceType.GetGenericTypeDefinition() == _type );
                bool success = true;
                var args = instanceType.GetGenericArguments();
                var arguments = new (IPocoGenericParameter Parameter, IPocoType Type)[args.Length];
                for( int iP = 0; iP < _parameters.Length; iP++ )
                {
                    GenParam? p = _parameters[iP];
                    var propertyType = instanceType.GetProperty( p._typePropName, BindingFlags.Public | BindingFlags.Static );
                    if( propertyType == null )
                    {
                        monitor.Error( $"Generic interface '{_type:N}' must define " +
                                       $"'[AutoImplementationClaim] public static {p.Name} {p._typePropName} => default!;' property. " +
                                       $"This is required for type analysis." );
                        success = false;
                    }
                    if( success )
                    {
                        var t = typeSystem.Register( monitor, propertyType! );
                        if( t != null )
                        {
                            arguments[iP] = (p, t);
                        }
                        else
                        {
                            success = false;
                        }
                    }
                }
                return success ? arguments : null;
            }
        }
    }

}
