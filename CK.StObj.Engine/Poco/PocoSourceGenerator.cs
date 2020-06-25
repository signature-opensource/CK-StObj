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
    public static class PocoSourceGenerator
    {
        class Module : ICodeGeneratorModule
        {
            readonly IPocoSupportResult _r;

            public Module( IPocoSupportResult r )
            {
                _r = r;
            }

            public IReadOnlyList<SyntaxTree> Rewrite( IReadOnlyList<SyntaxTree> trees ) => trees;

            public void Inject( ICodeWorkspace code )
            {
                if( _r.AllInterfaces.Count == 0 ) return;
                var b = code.Global
                                .FindOrCreateNamespace( _r.FinalFactory.Namespace )
                                .EnsureUsing( "System" );
                foreach( var root in _r.Roots )
                {
                    var tB = b.CreateType( t => t.Append( "class " )
                                                 .Append( root.PocoClass.Name )
                                                 .Append( " : " )
                                                 .Append( root.Interfaces.Select( i => i.PocoInterface.ToCSharpName() ) ) );
                    IFunctionScope defaultCtorB = null;

                    foreach( var p in root.PocoClass.GetProperties() )
                    {
                        tB.Append( "public " ).AppendCSharpName( p.PropertyType ).Space().Append( p.Name ).Append( "{get;" );
                        if( p.CanWrite ) tB.Append( "set;" );
                        tB.Append( "}" ).NewLine();
                        if( !p.CanWrite )
                        {
                            Type propType = p.PropertyType;
                            if( _r.AllInterfaces.TryGetValue( propType, out IPocoInterfaceInfo info ) )
                            {
                                if( defaultCtorB == null ) defaultCtorB = tB.CreateFunction( $"public {root.PocoClass.Name}()" );
                                defaultCtorB.Append( p.Name ).Append( " = new " ).Append( info.Root.PocoClass.Name ).Append( "();" ).NewLine();
                            }
                            else if( propType.IsGenericType )
                            {
                                Type genType = propType.GetGenericTypeDefinition();
                                if( genType == typeof( IList<> ) || genType == typeof( List<> ) )
                                {
                                    tB.Append( " = new System.Collections.Generic.List<" ).AppendCSharpName( propType.GetGenericArguments()[0] ).Append( ">();" ).NewLine();
                                }
                                else if( genType == typeof( IDictionary<,> ) || genType == typeof( Dictionary<,> ) )
                                {
                                    tB.Append( " = new System.Collections.Generic.Dictionary<" )
                                                        .AppendCSharpName( propType.GetGenericArguments()[0] )
                                                        .Append( ',' )
                                                        .AppendCSharpName( propType.GetGenericArguments()[1] )
                                                        .Append( ">();" )
                                                        .NewLine();
                                }
                                else if( genType == typeof( ISet<> ) || genType == typeof( HashSet<> ) )
                                {
                                    tB.Append( " = new System.Collections.Generic.HashSet<" ).AppendCSharpName( propType.GetGenericArguments()[0] ).Append( ">();" ).NewLine();
                                }
                            }
                        }
                    }
                }
                var fB = b.CreateType( t => t.Append( "class " )
                                             .Append( _r.FinalFactory.Name )
                                             .Append( " : " )
                                             .Append( _r.AllInterfaces.Values.Select( i => i.PocoFactoryInterface.ToCSharpName() ) ) );
                foreach( var i in _r.AllInterfaces.Values )
                {
                    fB.AppendCSharpName( i.PocoInterface )
                      .Space()
                      .AppendCSharpName( i.PocoFactoryInterface )
                      .Append( ".Create() => new " ).AppendCSharpName( i.Root.PocoClass ).Append( "();" )
                      .NewLine();
                   fB.Append( "Type " )
                     .AppendCSharpName( i.PocoFactoryInterface )
                     .Append( ".PocoClassType => typeof(" ).AppendCSharpName( i.Root.PocoClass ).Append( ");" )
                     .NewLine();
                }
            }
        }

        /// <summary>
        /// Creates a source code module that implements a <see cref="IPocoSupportResult"/>.
        /// </summary>
        /// <param name="r">The poco description.</param>
        /// <returns>The source code module.</returns>
        public static ICodeGeneratorModule CreateModule( IPocoSupportResult r )
        {
            if( r == null ) throw new ArgumentNullException( nameof( r ) );
            return new Module( r );
        }
    }
}
