using CK.CodeGen;
using CK.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using CK.CodeGen.Abstractions;
using CK.Core;
using System.Diagnostics;
using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// Code source generator for <see cref="IPoco"/>.
    /// </summary>
    public class PocoSourceGenerator : AutoImplementorType
    {

        public override AutoImplementationResult Implement( IActivityMonitor monitor, Type classType, ICodeGenerationContext c, ITypeScope scope )
        {
            IPocoSupportResult r = c.Assembly.GetPocoSupportResult();

            scope.Append( "Dictionary<string,IPocoFactory> _factories = new Dictionary<string,IPocoFactory>( " ).Append( r.NamedRoots.Count ).Append( " );" )
                 .NewLine()
                 .Append( "public override IPocoFactory Find( string name ) => _factories.GetValueOrDefault( name );" ).NewLine()
                 .Append( "internal void Register( IPocoFactory f )" ).NewLine()
                 .Append( "{" ).NewLine()
                 .Append( "_factories.Add( f.Name, f );" ).NewLine()
                 .Append( "foreach( var n in f.PreviousNames ) _factories.Add( n, f );" ).NewLine()
                 .Append( "}" ).NewLine();

            if( r.AllInterfaces.Count == 0 ) return AutoImplementationResult.Success;

            foreach( var root in r.Roots )
            {
                // Poco class.
                var tB = c.Assembly.FindOrCreateAutoImplementedClass( monitor, root.PocoClass );
                // Always create the default constructor (empty) so that other code generators
                // can always find it.
                // We support the interface here: if other participants have already created this type, it is
                // up to us, here, to handle the "exact" type definition.
                IFunctionScope defaultCtorB = tB.CreateFunction( $"public {root.PocoClass.Name}()" );
                tB.TypeDefinition.BaseTypes.AddRange( root.Interfaces.Select( i => new ExtendedTypeName( i.PocoInterface.ToCSharpName() ) ) );

                foreach( var p in root.PropertyList )
                {
                    Type propType = p.PropertyType;
                    tB.Append( "public " ).AppendCSharpName( propType ).Space().Append( p.PropertyName ).Append( "{get;" );
                    // We always implement a setter except if we are auto instantiating the value and NO properties are writable.
                    if( !p.AutoInstantiated || p.HasDeclaredSetter ) tB.Append( "set;" );
                    tB.Append( "}" ).NewLine();
                    if( p.AutoInstantiated )
                    {
                        if( r.AllInterfaces.TryGetValue( propType, out IPocoInterfaceInfo info ) )
                        {
                            Debug.Assert( p.DefaultValueSource == null, "Poco with [DefaultValue] has raised an error." );
                            tB.Append( " = new " ).Append( info.Root.PocoClass.Name ).Append( "();" ).NewLine();
                        }
                        else if( propType.IsGenericType )
                        {
                            Type genType = propType.GetGenericTypeDefinition();
                            if( genType == typeof( IList<> ) || genType == typeof( List<> ) )
                            {
                                Debug.Assert( p.DefaultValueSource == null, "AutoInstantiated with [DefaultValue] has raised an error." );
                                tB.Append( " = new System.Collections.Generic.List<" ).AppendCSharpName( propType.GetGenericArguments()[0] ).Append( ">();" ).NewLine();
                            }
                            else if( genType == typeof( IDictionary<,> ) || genType == typeof( Dictionary<,> ) )
                            {
                                Debug.Assert( p.DefaultValueSource == null, "AutoInstantiated with [DefaultValue] has raised an error." );
                                tB.Append( " = new System.Collections.Generic.Dictionary<" )
                                                    .AppendCSharpName( propType.GetGenericArguments()[0] )
                                                    .Append( ',' )
                                                    .AppendCSharpName( propType.GetGenericArguments()[1] )
                                                    .Append( ">();" )
                                                    .NewLine();
                            }
                            else if( genType == typeof( ISet<> ) || genType == typeof( HashSet<> ) )
                            {
                                Debug.Assert( p.DefaultValueSource == null, "AutoInstantiated with [DefaultValue] has raised an error." );
                                tB.Append( " = new System.Collections.Generic.HashSet<" ).AppendCSharpName( propType.GetGenericArguments()[0] ).Append( ">();" ).NewLine();
                            }
                        }
                    }
                    if( p.DefaultValueSource != null )
                    {
                        tB.Append( " = " ).Append( p.DefaultValueSource ).Append( ";" ).NewLine();
                    }
                }

                // PocoFactory class.

                var tFB = c.Assembly.FindOrCreateAutoImplementedClass( monitor, root.PocoFactoryClass );

                tFB.Append( "public Type PocoClassType => typeof(" ).AppendCSharpName( root.PocoClass ).Append( ");" )
                   .NewLine();

                tFB.Append( "public IPoco Create() => new " ).AppendCSharpName( root.PocoClass ).Append( "();" )
                   .NewLine();

                tFB.Append( "public string Name => " ).AppendSourceString( root.Name ).Append( ";" )
                   .NewLine();

                tFB.Append( "public IReadOnlyList<string> PreviousNames => " ).AppendArray( root.PreviousNames ).Append( ";" )
                   .NewLine();

                tFB.Append( "void StObjConstruct( PocoDirectory d ) => ((PocoDirectory_CK)d).Register(this);" )
                   .NewLine();

                foreach( var i in root.Interfaces )
                {
                    tFB.TypeDefinition.BaseTypes.Add( new ExtendedTypeName( i.PocoFactoryInterface.ToCSharpName() ) );
                    tFB.AppendCSharpName( i.PocoInterface )
                       .Space()
                       .AppendCSharpName( i.PocoFactoryInterface )
                       .Append( ".Create() => new " ).AppendCSharpName( i.Root.PocoClass ).Append( "();" )
                       .NewLine();
                }
            }
            return AutoImplementationResult.Success;
        }


    }
}
