using CK.CodeGen;
using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using CK.Core;
using System.Diagnostics;

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
            // Let the PocoDirectory_CK be sealed.
            scope.Definition.Modifiers |= Modifiers.Sealed;

            IPocoSupportResult r = c.Assembly.GetPocoSupportResult();
            Debug.Assert( r == c.CurrentRun.ServiceContainer.GetService( typeof(IPocoSupportResult) ), "The PocoSupportResult is also available at the GeneratedBinPath." );

            // PocoDirectory_CK class.
            scope.GeneratedByComment().NewLine()
                 .FindOrCreateFunction( "internal PocoDirectory_CK()" )
                 .Append( "Instance = this;" ).NewLine();

            scope.Append( "internal static PocoDirectory_CK Instance;" ).NewLine()
                 // The _factories field 
                 .Append( "static readonly Dictionary<string,IPocoFactory> _factoriesN = new Dictionary<string,IPocoFactory>( " ).Append( r.NamedRoots.Count ).Append( " );" ).NewLine()
                 .Append( "static readonly Dictionary<Type,IPocoFactory> _factoriesT = new Dictionary<Type,IPocoFactory>( " ).Append( r.AllInterfaces.Count ).Append( " );" ).NewLine()
                 .Append( "public override IPocoFactory Find( string name ) => _factoriesN.GetValueOrDefault( name );" ).NewLine()
                 .Append( "public override IPocoFactory Find( Type t ) => _factoriesT.GetValueOrDefault( t );" ).NewLine()
                 .Append( "internal static void Register( IPocoFactory f )" ).OpenBlock()
                 .Append( "_factoriesN.Add( f.Name, f );" ).NewLine()
                 .Append( "foreach( var n in f.PreviousNames ) _factoriesN.Add( n, f );" ).NewLine()
                 .Append( "foreach( var i in f.Interfaces ) _factoriesT.Add( i, f );" ).NewLine()
                 .Append( "// The factory type itself is also registered. This enables to locate the Poco instance from its GetType()." ).NewLine()
                 .Append( "_factoriesT.Add( f.PocoClassType, f );" ).NewLine()
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
                //
                // This static internal field is an awful shortcut but it makes things simpler and more efficient
                // than looking up the factory in the DI (and downcasting it) each time we need it.
                // This simplification has been done for Cris Command implementation: a ICommand exposes
                // its ICommandModel: we used to inject the ICommandModel (that is the extended PocoFactory) in the ICommand
                // PocoClass ctor from the factory methods. It worked but it was complex... and, eventually, there
                // can (today) but most importantly there SHOULD, be only one StObjMap/Concrete generated code in an
                // assembly. Maybe one day, the StObj instances themselves can be made static (since they are some kind of
                // "absolute singletons").
                //
                // Note to myself: this "static shortcut" is valid because we are on a "final generation", not on a
                // local, per-module, intermediate, code generation like .Net 5 Code Generators.
                // How this kind of shortcuts could be implemented with .Net 5 Code Generators? It seems that it could but
                // there will be as many "intermediate statics" as there are "levels of assemblies"? Or, there will be only
                // one static (the first one) and the instance will be replaced by the subsequent assemblies? In all cases,
                // diamond problem will have to be ultimately resolved at the final leaf... Just like we do!
                // 
                tB.Append( "internal static " ).Append( tFB.Name ).Append( " _factory;")
                  .NewLine();
                tB.Append( "IPocoFactory IPocoGeneratedClass.Factory => _factory;" ).NewLine();
                
                // Always create the constructor so that other code generators
                // can always find it.
                // We support the interfaces here: if other participants have already created this type, it is
                // up to us, here, to handle the "exact" type definition.
                tB.Definition.BaseTypes.Add( new ExtendedTypeName( "IPocoGeneratedClass" ) );
                tB.Definition.BaseTypes.AddRange( root.Interfaces.Select( i => new ExtendedTypeName( i.PocoInterface.ToCSharpName() ) ) );

                IFunctionScope ctorB = tB.CreateFunction( $"public {root.PocoClass.Name}()" );

                foreach( var p in root.PropertyList )
                {
                    Type propType = p.PropertyType;
                    bool isUnionType = p.PropertyUnionTypes.Any();

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
                    Debug.Assert( !p.IsReadOnly || p.DefaultValueSource == null, "Readonly with [DefaultValue] has already raised an error." );
                   
                    if( p.IsReadOnly )
                    {
                        // Generates in constructor.
                        r.GenerateAutoInstantiatedNewAssignation( ctorB, fieldName, p.PropertyType );
                    }

                    tB.OpenBlock()
                      .Append( "get => " ).Append( fieldName ).Append( ";" ).NewLine();

                    if( !p.IsReadOnly )
                    {
                        tB.Append( "set" )
                          .OpenBlock();

                        bool isTechnicallyNullable = p.PropertyNullableTypeTree.Kind.IsTechnicallyNullable();
                        bool isNullable = p.PropertyNullableTypeTree.Kind.IsNullable();

                        if( isTechnicallyNullable && !isNullable )
                        {
                            tB.Append( "if( value == null ) throw new ArgumentNullException();" ).NewLine();
                        }

                        if( isUnionType )
                        {
                            if( isNullable )
                            {
                                tB.Append( "if( value != null )" )
                                  .OpenBlock();
                            }
                            tB.Append( "Type tV = value.GetType();" ).NewLine()
                                .Append( "if( !_c" ).Append( fieldName )
                                .Append( ".Any( t => t.IsAssignableFrom( tV ) ))" )
                                .OpenBlock()
                                .Append( "throw new ArgumentException( $\"Unexpected Type '{tV}' in UnionType. Allowed types are: " )
                                .Append( p.PropertyUnionTypes.Select( tU => tU.ToString() ).Concatenate() )
                                .Append( ".\");" )
                                .CloseBlock();
                            if( isNullable )
                            {
                                tB.CloseBlock();
                            }
                        }
                        tB.Append( fieldName ).Append( " = value;" )
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

                tFB.Append( "public IReadOnlyList<Type> Interfaces => " ).AppendArray( root.Interfaces.Select( i => i.PocoInterface ) ).Append( ";" )
                   .NewLine();

                tFB.CreateFunction( "public " + factoryClassName + "()" )
                    .Append( "PocoDirectory_CK.Register( this );" ).NewLine()
                    .Append( tB.Name ).Append( "._factory = this;" );

                foreach( var i in root.Interfaces )
                {
                    tFB.Definition.BaseTypes.Add( new ExtendedTypeName( i.PocoFactoryInterface.ToCSharpName() ) );
                    tFB.AppendCSharpName( i.PocoInterface, true, true, true )
                       .Space()
                       .AppendCSharpName( i.PocoFactoryInterface, true, true, true )
                       .Append( ".Create() => new " ).AppendCSharpName( i.Root.PocoClass, true, true, true ).Append( "();" )
                       .NewLine();
                }
            }
            return CSCodeGenerationResult.Success;
        }

    }
}
