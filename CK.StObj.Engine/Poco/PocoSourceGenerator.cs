using CK.CodeGen;
using CK.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using CK.CodeGen.Abstractions;
using CK.Core;

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
                    foreach( var p in root.PocoClass.GetProperties() )
                    {
                        tB.Append("public " ).AppendCSharpName( p.PropertyType ).Space().Append( p.Name ).Append( "{" );
                        tB.Append( "get;" );
                        if( p.CanWrite ) tB.Append( "set;" );
                        tB.Append( "}" ).NewLine();
                    }
                }
                var fB = b.CreateType( t => t.Append( "class " )
                                             .Append( _r.FinalFactory.Name )
                                             .Append( " : " )
                                             .Append( _r.AllInterfaces.Select( i => i.PocoFactoryInterface.ToCSharpName() ) ) );
                foreach( var i in _r.AllInterfaces )
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
