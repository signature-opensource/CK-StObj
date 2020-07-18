using CK.CodeGen;
using CK.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
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
        /// <summary>
        /// Generates the <paramref name="scope"/> that is the PocoDirectory_CK class and
        /// all the factories (<see cref="IPocoFactory"/> implementations) and the Poco class (<see cref="IPoco"/> implementations).
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="classType">The <see cref="PocoDirectory"/> type.</param>
        /// <param name="c">Code generation context.</param>
        /// <param name="scope">The PocoDirectory_CK type scope.</param>
        /// <returns>Always <see cref="AutoImplementationResult.Success"/>.</returns>
        public override AutoImplementationResult Implement( IActivityMonitor monitor, Type classType, ICodeGenerationContext c, ITypeScope scope )
        {
            Debug.Assert( scope.FullName == "CK.Core.PocoDirectory_CK", "We can use the PocoDirectory_CK type name to reference the PocoDirectory implementation." );

            IPocoSupportResult r = c.Assembly.GetPocoSupportResult();

            // PocoDirectory_CK class.
            scope.FindOrCreateFunction( "public PocoDirectory_CK()" )
                 .Append( "_factories = new Dictionary<string,IPocoFactory>( " ).Append( r.NamedRoots.Count ).Append( " );" );

            scope.Append( "Dictionary<string,IPocoFactory> _factories;" ).NewLine()
                 .Append( "public override IPocoFactory Find( string name ) => _factories.GetValueOrDefault( name );" ).NewLine()
                 .Append( "internal PocoDirectory_CK Register( IPocoFactory f )" ).OpenBlock()
                 .Append( "_factories.Add( f.Name, f );" ).NewLine()
                 .Append( "foreach( var n in f.PreviousNames ) _factories.Add( n, f );" ).NewLine()
                 .Append( "return this;" ).NewLine()
                 .CloseBlock();

            if( r.AllInterfaces.Count == 0 ) return AutoImplementationResult.Success;

            foreach( var root in r.Roots )
            {
                // PocoFactory class.
                var tFB = c.Assembly.FindOrCreateAutoImplementedClass( monitor, root.PocoFactoryClass );
                tFB.Definition.Modifiers |= Modifiers.Sealed;
                string factoryClassName = tFB.Definition.Name.Name;

                // Poco class.
                var tB = c.Assembly.FindOrCreateAutoImplementedClass( monitor, root.PocoClass );
                tB.Definition.Modifiers |= Modifiers.Sealed;

                // The factory field is private and its type is the exact class: extended code can refer to the _factory
                // and can have access to the factory extended code without cast.
                tB.Append( "readonly " ).Append( factoryClassName ).Append( " _factory;" ).NewLine();
                tB.Append( "public IPocoFactory Factory => _factory;" ).NewLine();
                
                // Always create the constructor so that other code generators
                // can always find it.
                // We support the interfaces here: if other participants have already created this type, it is
                // up to us, here, to handle the "exact" type definition.
                tB.Definition.BaseTypes.Add( new ExtendedTypeName( "IPocoClass" ) );
                tB.Definition.BaseTypes.AddRange( root.Interfaces.Select( i => new ExtendedTypeName( i.PocoInterface.ToCSharpName() ) ) );

                IFunctionScope ctorB = tB.CreateFunction( $"public {root.PocoClass.Name}( PocoDirectory_CK d )" );
                ctorB.Append( "_factory = d._f" ).Append( tFB.UniqueId ).Append( ';' ).NewLine();

                foreach( var p in root.PropertyList )
                {
                    Type propType = p.PropertyType;
                    // We always implement a setter except if we are auto instantiating the value and NO properties are writable.
                    bool generateSetter = !p.AutoInstantiated || p.HasDeclaredSetter;
                    bool isAutoProperty = p.PropertyUnionTypes.Count == 0;

                    var typeName = propType.ToCSharpName();
                    tB.Append( "public " ).Append( typeName ).Space().Append( p.PropertyName );
                    if( isAutoProperty )
                    {
                        tB.Append( "{ get;" );
                        if( generateSetter )
                        {
                            tB.Append( " set;" );
                        }
                        tB.Append( "}" );
                        if( p.AutoInstantiated )
                        {
                            // Generates in constructor.
                            r.GenerateAutoInstantiatedNewAssignation( ctorB, p.PropertyName, p.PropertyType, "d" );
                        }
                        Debug.Assert( !p.AutoInstantiated || p.DefaultValueSource == null, "AutoInstantiated with [DefaultValue] has already raised an error." );
                    }
                    else
                    {
                        Debug.Assert( !p.AutoInstantiated );
                        string fieldName = "_fA" + c.Assembly.NextUniqueNumber();
                        tB.OpenBlock()
                          .Append( "get => " ).Append( fieldName ).Append( ";" ).NewLine()
                          .Append( "set" )
                          .OpenBlock()
                          .Append( "if( value != null )" )
                          .OpenBlock()

                                .Append( "Type tV = value.GetType();" ).NewLine()
                                .Append( "if( !_c" ).Append( fieldName )
                                .Append( ".Any( t => t.IsAssignableFrom( tV ) ))" )
                                .OpenBlock()
                                .Append( "throw new ArgumentException( \"Invalid Type in UnionType\");" )
                                .CloseBlock()

                          .CloseBlock()
                          .Append( fieldName ).Append( " = value;" ).NewLine()
                          .CloseBlock()
                          .CloseBlock();
                        tB.Append( "static readonly Type[] _c" ).Append( fieldName ).Append( "=" ).AppendArray( p.PropertyUnionTypes ).Append( ";" ).NewLine();
                        tB.Append( typeName ).Space().Append( fieldName );
                        if( p.DefaultValueSource == null ) tB.Append( ";" );
                    }
                    if( p.DefaultValueSource != null )
                    {
                        tB.Append( " = " ).Append( p.DefaultValueSource ).Append( ";" );
                    }
                    tB.NewLine();
                }

                // PocoFactory class.

                // The PocoDirectory field is set by the StObjConstruct below. It is public and is 
                // typed with the generated class: extended code can use it without cast.
                tFB.Append( "public PocoDirectory_CK PocoDirectory;" ).NewLine();

                tFB.Append( "PocoDirectory IPocoFactory.PocoDirectory => PocoDirectory;" ).NewLine();

                tFB.Append( "public Type PocoClassType => typeof(" ).AppendCSharpName( root.PocoClass ).Append( ");" )
                   .NewLine();

                tFB.Append( "public IPoco Create() => new " ).AppendCSharpName( root.PocoClass ).Append( "( PocoDirectory );" )
                   .NewLine();

                tFB.Append( "public string Name => " ).AppendSourceString( root.Name ).Append( ";" )
                   .NewLine();

                tFB.Append( "public IReadOnlyList<string> PreviousNames => " ).AppendArray( root.PreviousNames ).Append( ";" )
                   .NewLine();

                // The StObjConstruct implementation registers the names AND the factory itself as a field of the directory implementation.
                // This enables a direct factory instance access, without any lookup in yet another dictionary.
                // The field is "internal" to mark it as a kind of "trick"...
                Debug.Assert( scope == c.Assembly.FindOrCreateAutoImplementedClass( monitor, typeof( PocoDirectory ) ), "We are implementing the PocoDirectory." );
                scope.Append( "internal " ).Append( tFB.FullName ).Append( " _f" ).Append( tFB.UniqueId ).Append( ';' ).NewLine();

                tFB.Append( "void StObjConstruct( PocoDirectory d )" ).OpenBlock()
                    .Append( "PocoDirectory = (PocoDirectory_CK)d;" ).NewLine()
                    .Append( "PocoDirectory.Register( this )._f" ).Append( tFB.UniqueId ).Append( " = this;" ).NewLine()
                    .CloseBlock();

                foreach( var i in root.Interfaces )
                {
                    tFB.Definition.BaseTypes.Add( new ExtendedTypeName( i.PocoFactoryInterface.ToCSharpName() ) );
                    tFB.AppendCSharpName( i.PocoInterface )
                       .Space()
                       .AppendCSharpName( i.PocoFactoryInterface )
                       .Append( ".Create() => new " ).AppendCSharpName( i.Root.PocoClass ).Append( "( PocoDirectory );" )
                       .NewLine();
                }
            }
            return AutoImplementationResult.Success;
        }
    }
}
