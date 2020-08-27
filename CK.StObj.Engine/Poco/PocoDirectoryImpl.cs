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
    /// Generates the implementation of the <see cref="PocoDirectory"/> abstract real object
    /// and all the Poco final classes.
    /// </summary>
    public class PocoDirectoryImpl : CSCodeGeneratorType
    {
        /// <summary>
        /// Generates the <paramref name="scope"/> that is the PocoDirectory_CK class and
        /// all the factories (<see cref="IPocoFactory"/> implementations) and the Poco class (<see cref="IPoco"/> implementations).
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="classType">The <see cref="PocoDirectory"/> type.</param>
        /// <param name="c">Code generation context.</param>
        /// <param name="scope">The PocoDirectory_CK type scope.</param>
        /// <returns>Always <see cref="CSCodeGenerationResult.Success"/>.</returns>
        public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
        {
            Debug.Assert( scope.FullName == "CK.Core.PocoDirectory_CK", "We can use the PocoDirectory_CK type name to reference the PocoDirectory implementation." );

            IPocoSupportResult r = c.Assembly.GetPocoSupportResult();

            // PocoDirectory_CK class.
            scope.FindOrCreateFunction( "internal PocoDirectory_CK()" )
                 .Append( "Instance = this;" );

            scope.Append( "internal static PocoDirectory_CK Instance;" ).NewLine()
                 .Append( "static readonly Dictionary<string,IPocoFactory> _factories = new Dictionary<string,IPocoFactory>( " ).Append( r.NamedRoots.Count ).Append( " );" ).NewLine()
                 .Append( "public override IPocoFactory Find( string name ) => _factories.GetValueOrDefault( name );" ).NewLine()
                 .Append( "internal static void Register( IPocoFactory f )" ).OpenBlock()
                 .Append( "_factories.Add( f.Name, f );" ).NewLine()
                 .Append( "foreach( var n in f.PreviousNames ) _factories.Add( n, f );" ).NewLine()
                 .CloseBlock();

            if( r.AllInterfaces.Count == 0 ) return CSCodeGenerationResult.Success;

            foreach( var root in r.Roots )
            {
                // PocoFactory class.
                var tFB = c.Assembly.FindOrCreateAutoImplementedClass( monitor, root.PocoFactoryClass );
                tFB.Definition.Modifiers |= Modifiers.Sealed;
                string factoryClassName = tFB.Definition.Name.Name;

                // Poco class.
                var tB = c.Assembly.FindOrCreateAutoImplementedClass( monitor, root.PocoClass );
                tB.Definition.Modifiers |= Modifiers.Sealed;

                // The Poco's static _factory field is internal and its type is the exact class: extended code
                // can refer to the _factory to access the factory extended code without cast.
                tB.Append( "internal static " ).Append( tFB.Name ).Append( " _factory;")
                  .NewLine();
                tB.Append( "IPocoFactory IPocoClass.Factory => _factory;" ).NewLine();
                
                // Always create the constructor so that other code generators
                // can always find it.
                // We support the interfaces here: if other participants have already created this type, it is
                // up to us, here, to handle the "exact" type definition.
                tB.Definition.BaseTypes.Add( new ExtendedTypeName( "IPocoClass" ) );
                tB.Definition.BaseTypes.AddRange( root.Interfaces.Select( i => new ExtendedTypeName( i.PocoInterface.ToCSharpName() ) ) );

                IFunctionScope ctorB = tB.CreateFunction( $"public {root.PocoClass.Name}()" );

                foreach( var p in root.PropertyList )
                {
                    Type propType = p.PropertyType;
                    // We always implement a setter except if we are auto instantiating the value and NONE of the properties are writable.
                    bool isUnionType = p.PropertyUnionTypes.Any();
                    bool generateSetter = !p.AutoInstantiated || p.HasDeclaredSetter || isUnionType;

                    var typeName = propType.ToCSharpName();
                    string fieldName = "_v" + p.Index;
                    tB.Append( typeName ).Space().Append( fieldName );
                    if( p.DefaultValueSource == null ) tB.Append( ";" );
                    else
                    {
                        tB.Append( " = " ).Append( p.DefaultValueSource ).Append( ";" );
                    }
                    tB.NewLine();

                    tB.Append( "public " ).Append( typeName ).Space().Append( p.PropertyName );
                    Debug.Assert( !p.AutoInstantiated || p.DefaultValueSource == null, "AutoInstantiated with [DefaultValue] has already raised an error." );
                   
                    if( p.AutoInstantiated )
                    {
                        // Generates in constructor.
                        r.GenerateAutoInstantiatedNewAssignation( ctorB, fieldName, p.PropertyType );
                    }

                    tB.OpenBlock()
                      .Append( "get => " ).Append( fieldName ).Append( ";" ).NewLine();

                    if( generateSetter )
                    {
                        tB.Append( "set" )
                          .OpenBlock();

                        bool isTechnicallyNullable = p.PropertyNullabilityInfo.Kind.IsTechnicallyNullable();
                        bool isEventuallyNullable = p.IsEventuallyNullable;

                        if( isTechnicallyNullable )
                        {
                            tB.Append( "if( value != null )" )
                              .OpenBlock();
                        }
                        if( isUnionType )
                        {
                            tB.Append( "Type tV = value.GetType();" ).NewLine()
                                .Append( "if( !_c" ).Append( fieldName )
                                .Append( ".Any( t => t.IsAssignableFrom( tV ) ))" )
                                .OpenBlock()
                                .Append( "throw new ArgumentException( \"Unexpected Type '{tV}' in UnionType\");" )
                                .CloseBlock();
                        }
                        if( isTechnicallyNullable )
                        {
                            tB.CloseBlock();
                            if( !isEventuallyNullable )
                            {
                                tB.Append( "else throw new ArgumentNullException();" ).NewLine();
                            }
                        }
                        tB.Append( fieldName ).Append( " = value;" ).NewLine()
                          .CloseBlock();
                    }
                    tB.CloseBlock();

                    if( isUnionType )
                    {
                        tB.Append( "static readonly Type[] _c" ).Append( fieldName ).Append( "=" ).AppendArray( p.PropertyUnionTypes.Select( u => u.Type ) ).Append( ";" ).NewLine();
                    }
                }

                // PocoFactory class.

                tFB.Append( "PocoDirectory IPocoFactory.PocoDirectory => PocoDirectory_CK.Instance;" ).NewLine();

                tFB.Append( "public Type PocoClassType => typeof(" ).Append( root.PocoClass.Name ).Append( ");" )
                   .NewLine();

                tFB.Append( "public IPoco Create() => new " ).Append( root.PocoClass.Name ).Append( "();" )
                   .NewLine();

                tFB.Append( "public string Name => " ).AppendSourceString( root.Name ).Append( ";" )
                   .NewLine();

                tFB.Append( "public IReadOnlyList<string> PreviousNames => " ).AppendArray( root.PreviousNames ).Append( ";" )
                   .NewLine();

                tFB.CreateFunction( "public " + factoryClassName + "()" )
                    .Append( "PocoDirectory_CK.Register( this );" ).NewLine()
                    .Append( tB.Name ).Append( "._factory = this;" );

                foreach( var i in root.Interfaces )
                {
                    tFB.Definition.BaseTypes.Add( new ExtendedTypeName( i.PocoFactoryInterface.ToCSharpName() ) );
                    tFB.AppendCSharpName( i.PocoInterface )
                       .Space()
                       .AppendCSharpName( i.PocoFactoryInterface )
                       .Append( ".Create() => new " ).AppendCSharpName( i.Root.PocoClass ).Append( "();" )
                       .NewLine();
                }
            }
            return CSCodeGenerationResult.Success;
        }
    }
}
