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
        /// <summary>
        /// Generates the code of the poco classes in the <see cref="IPocoSupportResult.FinalFactory"/>' namespace
        /// along with the final factory itself.
        /// </summary>
        /// <param name="code">The code workspace.</param>
        /// <param name="r">The poco analysis.</param>
        public static void Inject( ICodeWorkspace code, IPocoSupportResult r )
        {
            if( code == null ) throw new ArgumentNullException( nameof( code ) );
            if( r == null ) throw new ArgumentNullException( nameof( r ) );
            if( r.AllInterfaces.Count == 0 ) return;
            var b = code.Global
                            .FindOrCreateNamespace( r.FinalFactory.Namespace )
                            .EnsureUsing( "System" );
            foreach( var root in r.Roots )
            {
                var tB = b.CreateType( t => t.Append( "class " )
                                             .Append( root.PocoClass.Name )
                                             .Append( " : " )
                                             .Append( root.Interfaces.Select( i => i.PocoInterface.ToCSharpName() ) ) );
                IFunctionScope defaultCtorB = null;

                foreach( var p in root.PropertyList )
                {
                    Type propType = p.PropertyType;
                    tB.Append( "public " ).AppendCSharpName( propType ).Space().Append( p.PropertyName ).Append( "{get;" );
                    // We always implement a setter except if we are .
                    if( !p.AutoInstantiated ) tB.Append( "set;" );
                    tB.Append( "}" ).NewLine();
                    if( p.AutoInstantiated )
                    {
                        if( r.AllInterfaces.TryGetValue( propType, out IPocoInterfaceInfo info ) )
                        {
                            if( defaultCtorB == null ) defaultCtorB = tB.CreateFunction( $"public {root.PocoClass.Name}()" );
                            defaultCtorB.Append( p.PropertyName ).Append( " = new " ).Append( info.Root.PocoClass.Name ).Append( "();" ).NewLine();
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
                                         .Append( r.FinalFactory.Name )
                                         .Append( " : " )
                                         .Append( r.AllInterfaces.Values.Select( i => i.PocoFactoryInterface.ToCSharpName() ) ) );
            foreach( var i in r.AllInterfaces.Values )
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
}
