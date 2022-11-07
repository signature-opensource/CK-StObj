using CK.CodeGen;
using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using CK.Core;
using System.Diagnostics;
using static System.Formats.Asn1.AsnWriter;

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

            IPocoDirectory pocoDirectory = c.Assembly.GetPocoDirectory();
            IPocoTypeSystem pocoTypeSystem = c.Assembly.GetPocoTypeSystem();
            Debug.Assert( pocoDirectory == c.CurrentRun.ServiceContainer.GetService( typeof( IPocoDirectory ) ), "The IPocoDirectory is also available at the GeneratedBinPath." );
            Debug.Assert( pocoTypeSystem == c.CurrentRun.ServiceContainer.GetService( typeof( IPocoTypeSystem ) ), "The IPocoTypeSystem is also available at the GeneratedBinPath." );

            ImplementPocoRequiredSupport( monitor, pocoTypeSystem, scope.Workspace );

            // PocoDirectory_CK class.
            scope.GeneratedByComment().NewLine()
                 .FindOrCreateFunction( "internal PocoDirectory_CK()" )
                 .Append( "Instance = this;" ).NewLine();

            scope.Append( "internal static PocoDirectory_CK Instance;" ).NewLine()
                 // The _factories field 
                 .Append( "static readonly Dictionary<string,IPocoFactory> _factoriesN = new Dictionary<string,IPocoFactory>( " ).Append( pocoDirectory.NamedFamilies.Count ).Append( " );" ).NewLine()
                 .Append( "static readonly Dictionary<Type,IPocoFactory> _factoriesT = new Dictionary<Type,IPocoFactory>( " ).Append( pocoDirectory.AllInterfaces.Count ).Append( " );" ).NewLine()
                 .Append( "public override IPocoFactory Find( string name ) => _factoriesN.GetValueOrDefault( name );" ).NewLine()
                 .Append( "public override IPocoFactory Find( Type t ) => _factoriesT.GetValueOrDefault( t );" ).NewLine()
                 .Append( "internal static void Register( IPocoFactory f )" ).OpenBlock()
                 .Append( "_factoriesN.Add( f.Name, f );" ).NewLine()
                 .Append( "foreach( var n in f.PreviousNames ) _factoriesN.Add( n, f );" ).NewLine()
                 .Append( "foreach( var i in f.Interfaces ) _factoriesT.Add( i, f );" ).NewLine()
                 .Append( "// The factory type itself is also registered. This enables to locate the Poco instance from its GetType()." ).NewLine()
                 .Append( "_factoriesT.Add( f.PocoClassType, f );" ).NewLine()
                 .CloseBlock();

            if( pocoDirectory.AllInterfaces.Count == 0 ) return CSCodeGenerationResult.Success;

            foreach( var family in pocoDirectory.Families )
            {
                // PocoFactory class.
                var tFB = c.Assembly.FindOrCreateAutoImplementedClass( monitor, family.PocoFactoryClass );
                tFB.Definition.Modifiers |= Modifiers.Sealed;
                string factoryClassName = tFB.Definition.Name.Name;

                // Poco class.
                var tB = c.Assembly.FindOrCreateAutoImplementedClass( monitor, family.PocoClass );
                tB.Definition.Modifiers |= Modifiers.Sealed;

                var fieldPart = tB.CreatePart();

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
                fieldPart.Append( "internal static " ).Append( tFB.Name ).Append( " _factory;" ).NewLine();

                tB.Append( "IPocoFactory IPocoGeneratedClass.Factory => _factory;" ).NewLine();

                // Always create the constructor so that other code generators
                // can always find it.
                // We support the interfaces here: if other participants have already created this type, it is
                // up to us, here, to handle the "exact" type definition.
                tB.Definition.BaseTypes.Add( new ExtendedTypeName( "IPocoGeneratedClass" ) );
                tB.Definition.BaseTypes.AddRange( family.Interfaces.Select( i => new ExtendedTypeName( i.PocoInterface.ToCSharpName() ) ) );

                var pocoType = pocoTypeSystem.GetPrimaryPocoType( family.PrimaryInterface.PocoInterface );
                Debug.Assert( pocoType != null );

                IFunctionScope ctorB = tB.CreateFunction( $"public {family.PocoClass.Name}()" );
                ctorB.Append( pocoType.CSharpBodyConstructorSourceCode );

                foreach( var f in pocoType.Fields )
                {
                    // Creates the backing field.
                    fieldPart.Append( f.Type.CSharpName ).Space().Append( f.PrivateFieldName ).Append( ";" ).NewLine();
                    // Creates the property.
                    if( f.IsByRef )
                    {
                        // A ref property is only the return of the ref backing field.
                        tB.Append( "public ref " ).Append( f.Type.CSharpName ).Space().Append( f.Name )
                          .Append( " => ref " ).Append( f.PrivateFieldName ).Append( ";" ).NewLine();
                    }
                    else
                    {
                        // The getter is always the same.
                        tB.Append( "public " ).Append( f.Type.CSharpName ).Space().Append( f.Name );
                        if( f.IsReadOnly )
                        {
                            // Readonly doesn't require the "get".
                            tB.Append( " => " ).Append( f.PrivateFieldName ).Append( ";" ).NewLine();
                        }
                        else
                        {
                            // For writable properties we need the get/set. 
                            tB.OpenBlock()
                              .Append( "get => " ).Append( f.PrivateFieldName ).Append( ";" ).NewLine();

                            tB.Append( "set" )
                                .OpenBlock();
                            // Always generate the null check.
                            if( !f.Type.IsNullable && !f.Type.Type.IsValueType )
                            {
                                tB.Append( "Throw.CheckNotNullArgument( value );" ).NewLine();
                            }
                            // UnionType: check against the allowed types.
                            if( f.Type is IUnionPocoType uT )
                            {
                                Debug.Assert( f.Type.Kind == PocoTypeKind.UnionType );
                                // Generates the "static Type[] _vXXXAllowed" array.
                                fieldPart.Append( "static readonly Type[] " ).Append( f.PrivateFieldName ).Append( "Allowed = " )
                                         .AppendArray( uT.AllowedTypes.Select( u => u.Type ) ).Append( ";" ).NewLine();

                                if( f.Type.IsNullable ) tB.Append( "if( value != null )" ).OpenBlock();

                                // Generates the check.
                                tB.Append( "Type tV = value.GetType();" ).NewLine()
                                  .Append( "if( !" ).Append( f.PrivateFieldName ).Append( "Allowed" )
                                  .Append( ".Any( t => t.IsAssignableFrom( tV ) ) )" )
                                    .OpenBlock()
                                    .Append( "Throw.ArgumentException( $\"Unexpected Type '{tV.ToCSharpName()}' in UnionType. Allowed types are: '" )
                                    .Append( uT.AllowedTypes.Select( tU => tU.CSharpName ).Concatenate( "', '" ) )
                                    .Append( "'.\");" )
                                    .CloseBlock();

                                if( f.Type.IsNullable ) tB.CloseBlock();
                            }
                            tB.Append( f.PrivateFieldName ).Append( " = value;" )
                                .CloseBlock()
                              .CloseBlock();
                        }
                    }
                    //
                    foreach( var prop in family.PropertyList[f.Index].DeclaredProperties )
                    {
                        if( prop.PropertyType != f.Type.Type )
                        {
                            if( prop.PropertyType.IsByRef )
                            {
                                var pType = prop.PropertyType.GetElementType()!;
                                tB.Append( "ref " ).Append( pType.ToCSharpName() ).Space()
                                  .Append( prop.DeclaringType.ToCSharpName() ).Append( "." ).Append( f.Name ).Space()
                                  .Append( " => ref " ).Append( f.PrivateFieldName ).Append( ";" ).NewLine();
                            }
                            else
                            {
                                tB.Append( prop.PropertyType.ToCSharpName() ).Space()
                                  .Append( prop.DeclaringType.ToCSharpName() ).Append( "." ).Append( f.Name ).Space()
                                  .Append( " => " ).Append( f.PrivateFieldName ).Append( ";" ).NewLine();

                            }
                        }
                    }
                }

                // PocoFactory class.

                tFB.Append( "PocoDirectory IPocoFactory.PocoDirectory => PocoDirectory_CK.Instance;" ).NewLine();

                tFB.Append( "public Type PocoClassType => typeof(" ).Append( family.PocoClass.Name ).Append( ");" )
                   .NewLine();

                tFB.Append( "public Type PrimaryInterface => " ).AppendTypeOf( family.PrimaryInterface.PocoInterface ).Append( ";" )
                   .NewLine();

                tFB.Append( "public Type? ClosureInterface => " ).AppendTypeOf( family.ClosureInterface ).Append( ";" )
                   .NewLine();

                tFB.Append( "public bool IsClosedPoco => " ).Append( family.IsClosedPoco ).Append( ";" )
                   .NewLine();

                tFB.Append( "public IPoco Create() => new " ).Append( family.PocoClass.Name ).Append( "();" )
                   .NewLine();

                tFB.Append( "public string Name => " ).AppendSourceString( family.Name ).Append( ";" )
                   .NewLine();

                tFB.Append( "public IReadOnlyList<string> PreviousNames => " ).AppendArray( family.PreviousNames ).Append( ";" )
                   .NewLine();

                tFB.Append( "public IReadOnlyList<Type> Interfaces => " ).AppendArray( family.Interfaces.Select( i => i.PocoInterface ) ).Append( ";" )
                   .NewLine();

                tFB.CreateFunction( "public " + factoryClassName + "()" )
                    .Append( "PocoDirectory_CK.Register( this );" ).NewLine()
                    .Append( tB.Name ).Append( "._factory = this;" );

                foreach( var i in family.Interfaces )
                {
                    tFB.Definition.BaseTypes.Add( new ExtendedTypeName( i.PocoFactoryInterface.ToCSharpName() ) );
                    tFB.AppendCSharpName( i.PocoInterface, true, true, true )
                       .Space()
                       .AppendCSharpName( i.PocoFactoryInterface, true, true, true )
                       .Append( ".Create() => new " ).AppendCSharpName( i.Family.PocoClass, true, true, true ).Append( "();" )
                       .NewLine();
                }
            }
            return CSCodeGenerationResult.Success;
        }

        void ImplementPocoRequiredSupport( IActivityMonitor monitor, IPocoTypeSystem pocoTypeSystem, ICodeWorkspace workspace )
        {
            var ns = workspace.Global.FindOrCreateNamespace( PocoRequiredSupportType.Namespace );
            ns.GeneratedByComment();
            foreach( var t in pocoTypeSystem.RequiredSupportTypes )
            {
                switch( t )
                {
                    case PocoListRequiredSupport list: GeneratePocoList( monitor, ns, list ); break;
                    default: throw new NotSupportedException();
                }
            }
        }

        void GeneratePocoList( IActivityMonitor monitor, INamespaceScope ns, PocoListRequiredSupport list )
        {
            var pocoClassName = list.Type.FamilyInfo.PocoClass.FullName;
            var t = ns.CreateType( $"sealed class {list.TypeName} : List<{pocoClassName}>" );
            t.Append( "public bool IsReadOnly => false;" ).NewLine();
            foreach( var tA in list.Type.FamilyInfo.Interfaces )
            {
                t.Definition.BaseTypes.Add( new ExtendedTypeName( $"IList<{tA.CSharpName}>" ) );

                t.Append( tA.CSharpName ).Append( " IList<" ).Append( tA.CSharpName ).Append( ">.this[int index] {" )
                    .Append( "get => this[index]; set => this[index] = (" ).Append( pocoClassName ).Append( ")value; }" ).NewLine()
                 .Append( "void ICollection<" ).Append( tA.CSharpName ).Append( ">.Add( " ).Append( tA.CSharpName )
                    .Append( " item ) => Add( (" ).Append( pocoClassName ).Append( ")item );" ).NewLine()
                 .Append( "bool ICollection<" ).Append( tA.CSharpName ).Append( ">.Contains( " ).Append( tA.CSharpName )
                    .Append( " item ) => Contains( (" ).Append( pocoClassName ).Append( ")item );" ).NewLine()
                 .Append( "void ICollection<" ).Append( tA.CSharpName ).Append( ">.CopyTo( " )
                    .Append( tA.CSharpName ).Append( "[] array, int arrayIndex ) => CopyTo( (" ).Append( pocoClassName ).Append( "[])array, arrayIndex );" ).NewLine()
                .Append( "int IList<" ).Append( tA.CSharpName ).Append( ">.IndexOf( " ).Append( tA.CSharpName )
                    .Append( " item ) => IndexOf( (" ).Append( pocoClassName ).Append( ")item );" ).NewLine()
                .Append( "void IList<" ).Append( tA.CSharpName ).Append( ">.Insert( int index, " ).Append( tA.CSharpName )
                    .Append( " item ) => Insert( index, (" ).Append( pocoClassName ).Append( ")item );" ).NewLine()
                .Append( "bool ICollection<" ).Append( tA.CSharpName ).Append( ">.Remove( " ).Append( tA.CSharpName )
                    .Append( " item ) => Remove( (" ).Append( pocoClassName ).Append( ")item );" ).NewLine()
                .Append( "IEnumerator<" ).Append( tA.CSharpName ).Append( "> IEnumerable<" ).Append( tA.CSharpName ).Append( ">.GetEnumerator() => GetEnumerator();" )
                .NewLine();
            }

        }
    }
}
