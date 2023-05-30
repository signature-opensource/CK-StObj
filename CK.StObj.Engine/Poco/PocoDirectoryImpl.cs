using CK.CodeGen;
using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using CK.Core;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace CK.Setup
{

    /// <summary>
    /// Code source generator for <see cref="IPoco"/>.
    /// Generates the implementation of the <see cref="PocoDirectory"/> abstract real object
    /// and all the Poco final classes.
    /// </summary>
    public sealed class PocoDirectoryImpl : CSCodeGeneratorType, ILockedPocoTypeSystem
    {
        [AllowNull]
        IPocoTypeSystem _typeSystem;
        Action? _isLocked;
        int _lastRegistrationCount;

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
            Debug.Assert( scope.FullName == "CK.Core.PocoDirectory_CK", "We can use the CK.Core.PocoDirectory_CK type name to reference the PocoDirectory implementation." );
            // Let the PocoDirectory_CK be sealed.
            scope.Definition.Modifiers |= Modifiers.Sealed;

            IPocoDirectory pocoDirectory = c.Assembly.GetPocoDirectory();
            _typeSystem = c.Assembly.GetPocoTypeSystem();
            Debug.Assert( pocoDirectory == c.CurrentRun.ServiceContainer.GetService( typeof( IPocoDirectory ) ), "The IPocoDirectory is also available at the GeneratedBinPath." );
            Debug.Assert( _typeSystem == c.CurrentRun.ServiceContainer.GetService( typeof( IPocoTypeSystem ) ), "The IPocoTypeSystem is also available at the GeneratedBinPath." );

            // Adds this ILockedPocoTypeSystem to the DI container right now.
            // Once [WaitFor] attribute is implemented, this should be done when locking the type system.
            c.CurrentRun.ServiceContainer.Add<ILockedPocoTypeSystem>( this );
            // Catches the current registration count.
            _lastRegistrationCount = _typeSystem.AllTypes.Count;
            monitor.Trace( $"PocoTypeSystem has initially {_lastRegistrationCount} registered types." );

            // One can immediately generate the Poco related code: poco are registered
            // during PocoTypeSystem initialization. Only other types (collection, records, etc.)
            // can be registered later (this is typically the case of the ICommand<TResult> results).
            // Those extra types don't impact the Poco code.
            ImplementPocoRequiredSupport( monitor, _typeSystem, scope.Workspace );

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
                var tFB = c.Assembly.Code.Global.FindOrCreateAutoImplementedClass( monitor, family.PocoFactoryClass );
                tFB.Definition.Modifiers |= Modifiers.Sealed;
                string factoryClassName = tFB.Definition.Name.Name;

                // Poco class.
                var tB = c.Assembly.Code.Global.FindOrCreateAutoImplementedClass( monitor, family.PocoClass );
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

                var pocoType = _typeSystem.FindObliviousType<IPrimaryPocoType>( family.PrimaryInterface.PocoInterface );
                Debug.Assert( pocoType != null );

                IFunctionScope ctorB = tB.CreateFunction( $"public {family.PocoClass.Name}()" );
                ctorB.Append( pocoType.CSharpBodyConstructorSourceCode );

                foreach( var f in pocoType.Fields )
                {
                    // Creates the backing field.
                    if( f.FieldAccess == PocoFieldAccessKind.MutableCollection || f.FieldAccess == PocoFieldAccessKind.ReadOnly )
                    {
                        // If it can be readonly, it should be.
                        fieldPart.Append( "readonly " );
                    }
                    fieldPart.Append( f.Type.ImplTypeName ).Space().Append( f.PrivateFieldName ).Append( ";" ).NewLine();

                    // Creates the property.
                    if( f.FieldAccess == PocoFieldAccessKind.IsByRef )
                    {
                        // A ref property is only the return of the ref backing field.
                        tB.Append( "public ref " ).Append( f.Type.CSharpName ).Space().Append( f.Name )
                          .Append( " => ref " ).Append( f.PrivateFieldName ).Append( ";" ).NewLine();
                    }
                    else
                    {
                        // The getter is always the same.
                        tB.Append( "public " ).Append( f.Type.CSharpName ).Space().Append( f.Name );
                        if( f.FieldAccess != PocoFieldAccessKind.HasSetter )
                        {
                            Debug.Assert( f.FieldAccess == PocoFieldAccessKind.MutableCollection || f.FieldAccess == PocoFieldAccessKind.ReadOnly );
                            // Readonly and MutableCollection doesn't require the "get".
                            // This expose a public (read only) property that is required for MutableCollection but
                            // a little bit useless for pure ReadOnly. However we need an implementation of the property
                            // declared on the interface (we could have generated explicit implementations here but it
                            // would be overcomplicated).
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
                    // Finally, provide an explicit implementations of all the declared properties
                    // that are not satisfied by the final property type.
                    foreach( IExtPropertyInfo prop in family.PropertyList[f.Index].DeclaredProperties )
                    {
                        if( prop.Type != f.Type.Type )
                        {
                            if( prop.Type.IsByRef )
                            {
                                tB.Append( "ref " ).Append( prop.TypeCSharpName ).Space()
                                    .Append( prop.DeclaringType.ToCSharpName() ).Append( "." ).Append( f.Name ).Space()
                                    .Append( " => ref " ).Append( f.PrivateFieldName ).Append( ";" ).NewLine();
                            }
                            else if( prop.Type != f.Type.Type )
                            {
                                tB.Append( prop.TypeCSharpName ).Space()
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

                tFB.Append( "public IReadOnlyList<string> PreviousNames => " ).AppendArray( family.ExternalName?.PreviousNames ?? Array.Empty<string>() ).Append( ";" )
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
            return new CSCodeGenerationResult( nameof( CheckNoMoreRegisteredPocoTypes ) );
        }

        static void ImplementPocoRequiredSupport( IActivityMonitor monitor, IPocoTypeSystem pocoTypeSystem, ICodeWorkspace workspace )
        {
            var ns = workspace.Global.FindOrCreateNamespace( PocoRequiredSupportType.Namespace );
            ns.GeneratedByComment();
            foreach( var t in pocoTypeSystem.RequiredSupportTypes )
            {
                switch( t )
                {
                    case PocoListOrHashSetRequiredSupport listOrSet:
                        if( listOrSet.IsList ) GeneratePocoList( monitor, ns, listOrSet );
                        else GeneratePocoHashSet( monitor, ns, listOrSet );
                        break;
                    case PocoDictionaryRequiredSupport dic: GeneratePocoDictionary( monitor, ns, dic ); break;
                    default: throw new NotSupportedException();
                }
            }
        }

        static void GeneratePocoList( IActivityMonitor monitor, INamespaceScope ns, PocoListOrHashSetRequiredSupport list )
        {
            Debug.Assert( list.Type.ImplTypeName == list.Type.FamilyInfo.PocoClass.FullName, "Because generated type is not nested." );
            var pocoClassName = list.Type.ImplTypeName;
            Debug.Assert( pocoClassName != null );
            var t = ns.CreateType( $"sealed class {list.TypeName} : List<{pocoClassName}>" );
            t.Append( "public bool IsReadOnly => false;" ).NewLine();
            foreach( var tI in list.Type.FamilyInfo.Interfaces )
            {
                t.Definition.BaseTypes.Add( new ExtendedTypeName( $"IList<{tI.CSharpName}>" ) );

                AppendICollectionImpl( t, tI.CSharpName, pocoClassName );

                t.Append( tI.CSharpName ).Append( " IList<" ).Append( tI.CSharpName ).Append( ">.this[int index] {" )
                    .Append( "get => this[index]; set => this[index] = (" ).Append( pocoClassName ).Append( ")value; }" ).NewLine()
                .Append( "int IList<" ).Append( tI.CSharpName ).Append( ">.IndexOf( " ).Append( tI.CSharpName )
                    .Append( " item ) => IndexOf( (" ).Append( pocoClassName ).Append( ")item );" ).NewLine()
                .Append( "void IList<" ).Append( tI.CSharpName ).Append( ">.Insert( int index, " ).Append( tI.CSharpName )
                    .Append( " item ) => Insert( index, (" ).Append( pocoClassName ).Append( ")item );" ).NewLine();
            }

        }

        static void GeneratePocoHashSet( IActivityMonitor monitor, INamespaceScope ns, PocoListOrHashSetRequiredSupport set )
        {
            var t = set.Type;
            var pocoClassName = t.ImplTypeName;
            Debug.Assert( pocoClassName != null );
            string? nonNullablePocoClassNameWhenNullable = null;
            bool isNullable = t.IsNullable;
            if( isNullable )
            {
                nonNullablePocoClassNameWhenNullable = pocoClassName;
                pocoClassName += "?"; 
            }
            var typeScope = ns.CreateType( $"sealed class {set.TypeName} : HashSet<{pocoClassName}>" );
            typeScope.Append( "public bool IsReadOnly => false;" ).NewLine();
            foreach( var tI in t.FamilyInfo.Interfaces )
            {
                AppendICollectionImpl( typeScope, tI.CSharpName, pocoClassName );
                // IReadOnlySet<T> is implemented by the public methods of ISet<T>.
                typeScope.Definition.BaseTypes.Add( new ExtendedTypeName( $"IReadOnlySet<{tI.CSharpName}>" ) );
                typeScope.Definition.BaseTypes.Add( new ExtendedTypeName( $"ISet<{tI.CSharpName}>" ) );

                typeScope.Append( "bool ISet<" ).Append( tI.CSharpName ).Append( ">.Add( " ).Append( tI.CSharpName ).Append( " item ) => Add( (" ).Append( pocoClassName ).Append( ")item );" ).NewLine()

                 .Append( "void ISet<" ).Append( tI.CSharpName ).Append( ".ExceptWith( IEnumerable<" ).Append( tI.CSharpName )
                    .Append( "> other ) => ExceptWith( (IEnumerable<" ).Append( pocoClassName ).Append( ">)other );" ).NewLine()

                .Append( "void ISet<" ).Append( tI.CSharpName ).Append( ".IntersectWith( IEnumerable<" ).Append( tI.CSharpName )
                    .Append( "> other ) => IntersectWith( (IEnumerable<" ).Append( pocoClassName ).Append( ">)other );" ).NewLine()

                .Append( "void ISet<" ).Append( tI.CSharpName ).Append( ".SymmetricExceptWith( IEnumerable<" ).Append( tI.CSharpName )
                    .Append( "> other ) => SymmetricExceptWith( (IEnumerable<" ).Append( pocoClassName ).Append( ">)other );" ).NewLine()

                .Append( "void ISet<" ).Append( tI.CSharpName ).Append( ".UnionWith( IEnumerable<" ).Append( tI.CSharpName )
                    .Append( "> other ) => UnionWith( (IEnumerable<" ).Append( pocoClassName ).Append( ">)other );" ).NewLine();

                typeScope.Append( "public bool IsProperSubsetOf( IEnumerable<" ).Append( tI.CSharpName )
                    .Append( "> other ) => base.IsProperSubsetOf( (IEnumerable<" ).Append( pocoClassName ).Append( ">)other );" ).NewLine()

                  .Append( "public bool IsProperSupersetOf( IEnumerable<" ).Append( tI.CSharpName )
                    .Append( "> other ) => base.IsProperSupersetOf( (IEnumerable<" ).Append( pocoClassName ).Append( ">)other );" ).NewLine()

                  .Append( "public bool IsSubsetOf( IEnumerable<" ).Append( tI.CSharpName )
                    .Append( "> other ) => base.IsSubsetOf( (IEnumerable<" ).Append( pocoClassName ).Append( ">)other );" ).NewLine()

                  .Append( "public bool IsSupersetOf( IEnumerable<" ).Append( tI.CSharpName )
                    .Append( "> other ) => base.IsSupersetOf( (IEnumerable<" ).Append( pocoClassName ).Append( ">)other );" ).NewLine()

                  .Append( "public bool Overlaps( IEnumerable<" ).Append( tI.CSharpName )
                    .Append( "> other ) => base.Overlaps( (IEnumerable<" ).Append( pocoClassName ).Append( ">)other );" ).NewLine()

                  .Append( "public bool SetEquals( IEnumerable<" ).Append( tI.CSharpName )
                    .Append( "> other ) => base.SetEquals( (IEnumerable<" ).Append( pocoClassName ).Append( ">)other );" ).NewLine();
            }
            AppendReadOnly( typeScope, isNullable ? "object?" : "object", pocoClassName, nonNullablePocoClassNameWhenNullable );
            AppendReadOnly( typeScope, isNullable ? "IPoco?" : "IPoco", pocoClassName, nonNullablePocoClassNameWhenNullable );
            if( t.FamilyInfo.IsClosedPoco ) AppendReadOnly( typeScope, isNullable ? "IClosedPoco?" : "IClosedPoco", pocoClassName, nonNullablePocoClassNameWhenNullable );
            foreach( var a in t.AbstractTypes )
            {
                AppendReadOnly( typeScope, a.CSharpName, pocoClassName, nonNullablePocoClassNameWhenNullable );
            }
            
            static void AppendReadOnly( ITypeScope typeScope, string abstractTypeName, string pocoClassName, string? nonNullablePocoClassNameWhenNullable )
            {
                typeScope.Definition.BaseTypes.Add( new ExtendedTypeName( $"IReadOnlySet<{abstractTypeName}>" ) );

                typeScope.Append( "bool IReadOnlySet<" ).Append( abstractTypeName )
                    .Append( ">.Contains( " ).Append( abstractTypeName ).Append( " item ) => " );
                if( nonNullablePocoClassNameWhenNullable == null )
                {
                    typeScope.Append( "item is " ).Append( pocoClassName ).Append( " v && Contains( v );" ).NewLine();
                }
                else
                {
                    typeScope.Append( "(item is " ).Append( nonNullablePocoClassNameWhenNullable ).Append( " v && Contains( v )) || (item == null && Contains( null ));" ).NewLine();
                }

                typeScope.Append( "bool IReadOnlySet<" ).Append( abstractTypeName ).Append( ">.IsProperSubsetOf( IEnumerable<" ).Append( abstractTypeName ).Append( "> other ) => CovariantHelpers.IsProperSubsetOf( this, other );" ).NewLine()

                .Append( "bool IReadOnlySet<" ).Append( abstractTypeName ).Append( ">.IsProperSupersetOf( IEnumerable<" ).Append( abstractTypeName ).Append( "> other ) => CovariantHelpers.IsProperSupersetOf( this, other );" ).NewLine()

                .Append( "bool IReadOnlySet<" ).Append( abstractTypeName ).Append( ">.IsSubsetOf( IEnumerable<" ).Append( abstractTypeName ).Append( "> other ) => CovariantHelpers.IsSubsetOf( this, other );" ).NewLine()

                .Append( "bool IReadOnlySet<" ).Append( abstractTypeName ).Append( ">.IsSupersetOf( IEnumerable<" ).Append( abstractTypeName ).Append( "> other ) => CovariantHelpers.IsSupersetOf( this, other );" ).NewLine()

                .Append( "bool IReadOnlySet<" ).Append( abstractTypeName ).Append( ">.Overlaps( IEnumerable<" ).Append( abstractTypeName ).Append( "> other ) => CovariantHelpers.Overlaps( this, other );" ).NewLine()

                .Append( "bool IReadOnlySet<" ).Append( abstractTypeName ).Append( ">.SetEquals( IEnumerable<" ).Append( abstractTypeName ).Append( "> other ) => CovariantHelpers.SetEquals( this, other );" ).NewLine()

                .Append( "IEnumerator<" ).Append( abstractTypeName ).Append( "> IEnumerable<" ).Append( abstractTypeName ).Append( ">.GetEnumerator() => GetEnumerator();" ).NewLine();
            }

        }

        static void AppendICollectionImpl( ITypeScope t, string abstractTypeName, string pocoClassName )
        {
            t.Append( "void ICollection<" ).Append( abstractTypeName ).Append( ">.Add( " ).Append( abstractTypeName )
               .Append( " item ) => Add( (" ).Append( pocoClassName ).Append( ")item );" ).NewLine()
            .Append( "bool ICollection<" ).Append( abstractTypeName ).Append( ">.Contains( " ).Append( abstractTypeName )
               .Append( " item ) => Contains( (" ).Append( pocoClassName ).Append( ")item );" ).NewLine()
            .Append( "void ICollection<" ).Append( abstractTypeName ).Append( ">.CopyTo( " )
               .Append( abstractTypeName ).Append( "[] array, int arrayIndex ) => CopyTo( (" ).Append( pocoClassName ).Append( "[])array, arrayIndex );" ).NewLine()
           .Append( "bool ICollection<" ).Append( abstractTypeName ).Append( ">.Remove( " ).Append( abstractTypeName )
               .Append( " item ) => Remove( (" ).Append( pocoClassName ).Append( ")item );" ).NewLine()
           .Append( "IEnumerator<" ).Append( abstractTypeName ).Append( "> IEnumerable<" ).Append( abstractTypeName ).Append( ">.GetEnumerator() => GetEnumerator();" )
           .NewLine();
        }

        static void GeneratePocoDictionary( IActivityMonitor monitor, INamespaceScope ns, PocoDictionaryRequiredSupport dic )
        {
            var pocoClassName = dic.Type.ImplTypeName;
            var typeScope = ns.CreateType( $"sealed class {dic.TypeName} : Dictionary<{dic.Key.CSharpName},{pocoClassName}>" );
            typeScope.Append( @"
bool TGV<TOut>( TKey key, out TOut? value ) where TOut : class
{
    if( base.TryGetValue( key, out var v ) )
    {
        value = Unsafe.As<TOut>( v );
        return true;
    }
    value = null;
    return false;
}
public bool IsReadOnly => false;" ).NewLine();

            AppendIReadOnly( typeScope, dic, "object", pocoClassName );
            AppendIReadOnly( typeScope, dic, "IPoco", pocoClassName );
            if( dic.Type.FamilyInfo.IsClosedPoco ) AppendIReadOnly( typeScope, dic, "IClosedPoco", pocoClassName );
            foreach( var tI in dic.Type.FamilyInfo.Interfaces )
            {
                AppendIReadOnly( typeScope, dic, tI.CSharpName, pocoClassName );
                typeScope.Definition.BaseTypes.Add( new ExtendedTypeName( $"IDictionary<{dic.Key.CSharpName},{tI.CSharpName}>" ) );

                typeScope.Append( "ICollection<TKey> IDictionary<TKey, " ).Append( tI.CSharpName ).Append( ">.Keys => Keys;" ).NewLine();
                typeScope.Append( "ICollection<" ).Append( tI.CSharpName ).Append( "> IDictionary<TKey, " ).Append( tI.CSharpName )
                    .Append( ">.Values => Unsafe.As<ICollection<" ).Append( tI.CSharpName ).Append( ">>( Values );" ).NewLine();
                typeScope.Append( "IThing IDictionary<TKey, " ).Append( tI.CSharpName )
                    .Append( ">.this[TKey key] { get => this[key]; set => this[key] = (" ).Append( pocoClassName ).Append( ")value; }" ).NewLine();
                typeScope.Append( "void IDictionary<TKey, " ).Append( tI.CSharpName ).Append( ">.Add( TKey key, " )
                    .Append( tI.CSharpName ).Append( " value ) => Add( key, (" ).Append( pocoClassName ).Append( ")value );" ).NewLine();
                typeScope.Append( "void ICollection<KeyValuePair<TKey, " ).Append( tI.CSharpName ).Append( ">>.Add( KeyValuePair<TKey, " ).Append( tI.CSharpName )
                    .Append( "> item ) => Add( item.Key, (" ).Append( pocoClassName ).Append( ")item.Value );" ).NewLine();

                typeScope.Append( "bool ICollection<KeyValuePair<TKey, " ).Append( tI.CSharpName ).Append( ">>.Contains( KeyValuePair<TKey, " ).Append( tI.CSharpName ).Append( "> item ) => base.TryGetValue( item.Key, out var v ) && v == item.Value;" ).NewLine();

                typeScope.Append( "void ICollection<KeyValuePair<TKey, " ).Append( tI.CSharpName ).Append( ">>.CopyTo( KeyValuePair<TKey, " ).Append( tI.CSharpName ).Append( ">[] array, int arrayIndex )" ).NewLine()
                    .Append( " => ((ICollection<KeyValuePair<TKey, " ).Append( pocoClassName ).Append( ">>)this).CopyTo( Unsafe.As<KeyValuePair<TKey, " ).Append( pocoClassName ).Append( ">[]>( array ), arrayIndex );" ).NewLine();

                typeScope.Append( "bool ICollection<KeyValuePair<TKey, " ).Append( tI.CSharpName ).Append( ">>.Remove( KeyValuePair<TKey, " ).Append( tI.CSharpName ).Append( "> item )" ).NewLine()
                    .Append( "=> ((ICollection<KeyValuePair<TKey, " ).Append( pocoClassName ).Append( ">>)this).Remove( new KeyValuePair<TKey, " ).Append( pocoClassName ).Append( ">( item.Key, (" ).Append( pocoClassName ).Append( ")item.Value ) );" ).NewLine();

            }
            foreach( var a in dic.Type.AbstractTypes )
            {
                AppendIReadOnly( typeScope, dic, a.CSharpName, pocoClassName );
            }

            static void AppendIReadOnly( ITypeScope t, PocoDictionaryRequiredSupport dic, string abstractTypeName, string pocoClassName )
            {
                t.Definition.BaseTypes.Add( new ExtendedTypeName( $"IReadOnlyDictionary<{dic.Key.CSharpName},{abstractTypeName}>" ) );
                t.Append( abstractTypeName ).Append( " IReadOnlyDictionary<TKey, " ).Append( abstractTypeName ).Append( ">.this[TKey key] => this[key];" ).NewLine();
                t.Append( "IEnumerable<TKey> IReadOnlyDictionary<TKey, " ).Append( abstractTypeName ).Append( ">.Keys => Keys;" ).NewLine();
                t.Append( "IEnumerable<" ).Append( abstractTypeName ).Append( "> IReadOnlyDictionary<TKey, " ).Append( abstractTypeName ).Append( ">.Values => Values;" ).NewLine();
                t.Append( "public bool TryGetValue( TKey key, out " ).Append( abstractTypeName ).Append( " value ) => TGV( key, out value );" ).NewLine();

                t.Append( "IEnumerator<KeyValuePair<TKey, " ).Append( abstractTypeName ).Append( ">> IEnumerable<KeyValuePair<TKey," ).Append( abstractTypeName ).Append( ">>.GetEnumerator()" ).NewLine()
                 .Append( "=> ((IEnumerable<KeyValuePair<TKey, " ).Append( pocoClassName ).Append( ">>)this).Select( kv => KeyValuePair.Create( kv.Key, (" ).Append( abstractTypeName ).Append( ")kv.Value ) ).GetEnumerator();" ).NewLine();
            }
        }


        CSCodeGenerationResult CheckNoMoreRegisteredPocoTypes( IActivityMonitor monitor )
        {
            var newCount = _typeSystem.AllTypes.Count;
            if( newCount != _lastRegistrationCount )
            {
                monitor.Trace( $"PocoTypeSystem has {newCount - _lastRegistrationCount} new types. Deferring Lock." );
                _lastRegistrationCount = newCount;
                return new CSCodeGenerationResult( nameof( CheckNoMoreRegisteredPocoTypes ) );
            }
            using( monitor.OpenInfo( $"PocoTypeSystem has no new types, code generation that requires all the Poco types to be known can start." ) )
            {
                _typeSystem.Lock( monitor );
                try
                {
                    _isLocked?.Invoke();
                }
                catch( Exception ex )
                {
                    monitor.Error( "While raising ILockedPocoTypeSystem.IsLocked event.", ex );
                    return CSCodeGenerationResult.Failed;
                }
            }
            return CSCodeGenerationResult.Success;
        }

        IPocoTypeSystem ILockedPocoTypeSystem.TypeSystem => _typeSystem;

        event Action ILockedPocoTypeSystem.IsLocked
        {
            add => _isLocked += value;
            remove => _isLocked -= value;
        }

    }
}
