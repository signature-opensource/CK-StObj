using CK.CodeGen;
using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using CK.Core;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CK.Setup
{

    /// <summary>
    /// Code source generator for <see cref="IPoco"/>.
    /// Generates the implementation of the <see cref="PocoDirectory"/> abstract real object
    /// and all the Poco final classes and 
    /// </summary>
    public sealed class PocoDirectoryImpl : CSCodeGeneratorType
    {
        [AllowNull] IPocoTypeSystemBuilder _typeSystemBuilder;
        [AllowNull] ICSCodeGenerationContext _context;
        [AllowNull] ITypeScopePart _finalTypeIndex;
        int _lastRegistrationCount;

        /// <summary>
        /// Generates the <paramref name="scope"/> that is the PocoDirectory_CK class and
        /// all the factories (<see cref="IPocoFactory"/> implementations) and the Poco class (<see cref="IPoco"/> implementations).
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="classType">The <see cref="PocoDirectory"/> type.</param>
        /// <param name="c">Code generation context.</param>
        /// <param name="scope">The PocoDirectory_CK type scope.</param>
        /// <returns>
        /// Always a continuation on a private CheckNoMoreRegisteredPocoTypes that monitors the IPocoTypeSystemBuilder for new types
        /// and calls <see cref="IPocoTypeSystemBuilder.Lock()"/> when no new types appeared.
        /// </returns>
        public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
        {
            Throw.DebugAssert( "We can use the CK.Core.PocoDirectory_CK type name to reference the PocoDirectory implementation.",
                                scope.FullName == "CK.Core.PocoDirectory_CK" );
            // Let the PocoDirectory_CK be sealed.
            scope.Definition.Modifiers |= Modifiers.Sealed;

            _context = c;
            _typeSystemBuilder = c.Assembly.GetPocoTypeSystemBuilder();
            Throw.DebugAssert( "The IPocoTypeSystem is also available at the GeneratedBinPath.", _typeSystemBuilder == c.CurrentRun.ServiceContainer.GetService( typeof( IPocoTypeSystemBuilder ) ) );

            IPocoDirectory pocoDirectory = _typeSystemBuilder.PocoDirectory;
            // Catches the current registration count.
            _lastRegistrationCount = _typeSystemBuilder.Count;
            monitor.Trace( $"PocoTypeSystem has initially {_lastRegistrationCount} registered types." );

            // One can immediately generate the Poco related code: poco are registered
            // during PocoTypeSystem initialization. Only other types (collection, records, etc.)
            // can be registered later.
            // Those extra types don't impact the Poco code.
            ImplementPocoRequiredSupport( monitor, _typeSystemBuilder, scope.Workspace );

            // Finds or creates the PocoDirectory_CK class.
            scope.GeneratedByComment().NewLine()
                 .FindOrCreateFunction( "internal PocoDirectory_CK()" )
                 .Append( "Instance = this;" ).NewLine();

            scope.Append( "internal static PocoDirectory_CK Instance;" ).NewLine()
                 // The _factories field 
                 .Append( "static readonly Dictionary<string,IPocoFactory> _factoriesN = new Dictionary<string,IPocoFactory>( " ).Append( pocoDirectory.NamedFamilies.Count ).Append( " );" ).NewLine()
                 .Append( "static readonly Dictionary<Type,IPocoFactory> _factoriesT = new Dictionary<Type,IPocoFactory>( " ).Append( pocoDirectory.AllInterfaces.Count ).Append( " );" ).NewLine()
                 .CreatePart( out _finalTypeIndex )
                 .Append( "public override IPocoFactory Find( string name ) => _factoriesN.GetValueOrDefault( name );" ).NewLine()
                 .Append( "public override IPocoFactory Find( Type t ) => _factoriesT.GetValueOrDefault( t );" ).NewLine()
                 .Append( "internal static void Register( IPocoFactory f )" )
                 .OpenBlock()
                 .Append( "_factoriesN.Add( f.Name, f );" ).NewLine()
                 .Append( "foreach( var n in f.PreviousNames ) _factoriesN.Add( n, f );" ).NewLine()
                 .Append( "foreach( var i in f.Interfaces ) _factoriesT.Add( i, f );" ).NewLine()
                 .Append( "// The factory type itself is also registered. This enables to locate the Poco instance from its GetType()." ).NewLine()
                 .Append( "_factoriesT.Add( f.PocoClassType, f );" ).NewLine()
                 .CloseBlock();

            // If there is no families, then we'll generate nothing but we
            // wait for any other type registration in the builder in order to
            // lock it and publish the type system.
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

                var pocoType = _typeSystemBuilder.FindByType<IPrimaryPocoType>( family.PrimaryInterface.PocoInterface );
                Throw.DebugAssert( pocoType != null );

                IFunctionScope ctorB = tB.CreateFunction( $"public {family.PocoClass.Name}()" );
                ctorB.Append( pocoType.CSharpBodyConstructorSourceCode );

                foreach( var f in pocoType.Fields )
                {
                    // Creates the backing field.
                    if( f.FieldAccess == PocoFieldAccessKind.MutableReference || f.FieldAccess == PocoFieldAccessKind.AbstractReadOnly )
                    {
                        // Since it can be readonly, let it be.
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
                            Debug.Assert( f.FieldAccess == PocoFieldAccessKind.MutableReference || f.FieldAccess == PocoFieldAccessKind.AbstractReadOnly );
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
                                    .Append( "Throw.ArgumentException( \"value\", $\"Unexpected Type '{tV.ToCSharpName()}' in UnionType. Allowed types are: '" )
                                    .Append( uT.AllowedTypes.Select( tU => tU.CSharpName ).Concatenate( "', '" ) )
                                    .Append( "'.\");" )
                                    .CloseBlock();
                                if( f.Type.IsNullable ) tB.CloseBlock();
                                tB.Append( f.PrivateFieldName ).Append( " = value;" );
                            }
                            else
                            {
                                tB.Append( f.PrivateFieldName );
                                if( f.Type.CSharpName != f.Type.ImplTypeName )
                                {
                                    tB.Append( " = (" ).Append( f.Type.ImplTypeName ).Append( ")value;" );
                                }
                                else
                                {
                                    tB.Append( " = value;" );
                                }
                            }
                            tB.CloseBlock()
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
                            else
                            {
                                tB.Append( prop.TypeCSharpName ).Space()
                                  .Append( prop.DeclaringType.ToCSharpName() ).Append( "." ).Append( f.Name )
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

        static void ImplementPocoRequiredSupport( IActivityMonitor monitor, IPocoTypeSystemBuilder pocoTypeSystem, ICodeWorkspace workspace )
        {
            var ns = workspace.Global.FindOrCreateNamespace( IPocoRequiredSupportType.Namespace );
            using( ns.Region() )
            {
                foreach( var t in pocoTypeSystem.RequiredSupportTypes )
                {
                    switch( t )
                    {
                        case IPocoListOrHashSetRequiredSupport listOrSet:
                            if( listOrSet.IsList ) GeneratePocoList( monitor, ns, listOrSet );
                            else GeneratePocoHashSet( monitor, ns, listOrSet );
                            break;
                        case IPocoDictionaryRequiredSupport dic:
                            GeneratePocoDictionary( monitor, ns, dic );
                            break;
                        case IPocoHashSetOfAbstractOrBasicRefRequiredSupport set:
                            GenerateHashSetOfAbstractOrBasicRef( ns, set );
                            break;
                        case IPocoDictionaryOfAbstractOrBasicRefRequiredSupport dic:
                            GeneratePocoDictionaryOfAbstractBasicRef( monitor, ns, dic );
                            break;
                        default: throw new NotSupportedException();
                    }
                }
            }
        }

        static void GeneratePocoList( IActivityMonitor monitor, INamespaceScope ns, IPocoListOrHashSetRequiredSupport list )
        {
            Debug.Assert( list.ItemType.ImplTypeName == list.ItemType.FamilyInfo.PocoClass.FullName, "Because generated type is not nested." );
            var pocoClassName = list.ItemType.ImplTypeName;
            Debug.Assert( pocoClassName != null );
            var t = ns.CreateType( $"sealed class {list.TypeName} : List<{pocoClassName}>" );
            t.Append( "public bool IsReadOnly => false;" ).NewLine();
            foreach( var tI in list.ItemType.FamilyInfo.Interfaces )
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

        static void GeneratePocoHashSet( IActivityMonitor monitor, INamespaceScope ns, IPocoListOrHashSetRequiredSupport set )
        {
            var actualTypeName = set.ItemType.ImplTypeName;
            ITypeScope typeScope = CreateHashSetType( ns,
                                                      set,
                                                      set.ItemType.IsNullable,
                                                      ref actualTypeName,
                                                      out var nonNullableActualTypeNameIfNullable );
            // Implements the family: ISet<> support for all of them.
            foreach( var tI in set.ItemType.FamilyInfo.Interfaces )
            {
                AppendICollectionImpl( typeScope, tI.CSharpName, actualTypeName );
                // IReadOnlySet<T> is implemented by the public methods of ISet<T> except the IReadOnlySet<T>.Contains( T item ).
                typeScope.Definition.BaseTypes.Add( new ExtendedTypeName( $"IReadOnlySet<{tI.CSharpName}>" ) );
                typeScope.Definition.BaseTypes.Add( new ExtendedTypeName( $"ISet<{tI.CSharpName}>" ) );

                AppendIReadOnlySetContains( typeScope, tI.CSharpName, actualTypeName, nonNullableActualTypeNameIfNullable );

                typeScope.Append( "bool ISet<" ).Append( tI.CSharpName ).Append( ">.Add( " ).Append( tI.CSharpName ).Append( " item ) => Add( (" ).Append( actualTypeName ).Append( ")item );" ).NewLine()

                 .Append( "void ISet<" ).Append( tI.CSharpName ).Append( ">.ExceptWith( IEnumerable<" ).Append( tI.CSharpName )
                    .Append( "> other ) => ExceptWith( (IEnumerable<" ).Append( actualTypeName ).Append( ">)other );" ).NewLine()

                .Append( "void ISet<" ).Append( tI.CSharpName ).Append( ">.IntersectWith( IEnumerable<" ).Append( tI.CSharpName )
                    .Append( "> other ) => IntersectWith( (IEnumerable<" ).Append( actualTypeName ).Append( ">)other );" ).NewLine()

                .Append( "void ISet<" ).Append( tI.CSharpName ).Append( ">.SymmetricExceptWith( IEnumerable<" ).Append( tI.CSharpName )
                    .Append( "> other ) => SymmetricExceptWith( (IEnumerable<" ).Append( actualTypeName ).Append( ">)other );" ).NewLine()

                .Append( "void ISet<" ).Append( tI.CSharpName ).Append( ">.UnionWith( IEnumerable<" ).Append( tI.CSharpName )
                    .Append( "> other ) => UnionWith( (IEnumerable<" ).Append( actualTypeName ).Append( ">)other );" ).NewLine();

                typeScope.Append( "public bool IsProperSubsetOf( IEnumerable<" ).Append( tI.CSharpName )
                    .Append( "> other ) => base.IsProperSubsetOf( (IEnumerable<" ).Append( actualTypeName ).Append( ">)other );" ).NewLine()

                  .Append( "public bool IsProperSupersetOf( IEnumerable<" ).Append( tI.CSharpName )
                    .Append( "> other ) => base.IsProperSupersetOf( (IEnumerable<" ).Append( actualTypeName ).Append( ">)other );" ).NewLine()

                  .Append( "public bool IsSubsetOf( IEnumerable<" ).Append( tI.CSharpName )
                    .Append( "> other ) => base.IsSubsetOf( (IEnumerable<" ).Append( actualTypeName ).Append( ">)other );" ).NewLine()

                  .Append( "public bool IsSupersetOf( IEnumerable<" ).Append( tI.CSharpName )
                    .Append( "> other ) => base.IsSupersetOf( (IEnumerable<" ).Append( actualTypeName ).Append( ">)other );" ).NewLine()

                  .Append( "public bool Overlaps( IEnumerable<" ).Append( tI.CSharpName )
                    .Append( "> other ) => base.Overlaps( (IEnumerable<" ).Append( actualTypeName ).Append( ">)other );" ).NewLine()

                  .Append( "public bool SetEquals( IEnumerable<" ).Append( tI.CSharpName )
                    .Append( "> other ) => base.SetEquals( (IEnumerable<" ).Append( actualTypeName ).Append( ">)other );" ).NewLine();
            }
            AppendIReadOnlySetSupport( actualTypeName,
                                       nonNullableActualTypeNameIfNullable,
                                       typeScope,
                                       isIPoco: true,
                                       baseTypes: set.ItemType.AbstractTypes );
        }

        static void GenerateHashSetOfAbstractOrBasicRef( INamespaceScope ns, IPocoHashSetOfAbstractOrBasicRefRequiredSupport set )
        {
            var actualTypeName = set.ItemType.CSharpName;
            ITypeScope typeScope = CreateHashSetType( ns,
                                                      set,
                                                      set.ItemType.IsNullable,
                                                      ref actualTypeName,
                                                      out var nonNullableActualTypeNameIfNullable );

            GetAllBaseTypes( set.ItemType, out bool isIPoco, out IEnumerable<IPocoType> baseTypes );
            AppendIReadOnlySetSupport( actualTypeName,
                                       nonNullableActualTypeNameIfNullable,
                                       typeScope,
                                       isIPoco,
                                       baseTypes );
        }

        static void GetAllBaseTypes( IPocoType itemType, out bool isIPoco, out IEnumerable<IPocoType> baseTypes )
        {
            Throw.DebugAssert( itemType is IAbstractPocoType or IBasicRefPocoType );
            if( itemType is IAbstractPocoType a )
            {
                baseTypes = a.AllGeneralizations;
                isIPoco = true;
            }
            else
            {
                baseTypes = Unsafe.As<IBasicRefPocoType>( itemType ).BaseTypes;
                isIPoco = false;
            }
        }

        static ITypeScope CreateHashSetType( INamespaceScope ns,
                                             IPocoRequiredSupportType supportType,
                                             bool isNullable,
                                             ref string actualTypeName,
                                             out string? nonNullableActualTypeNameIfNullable )
        {
            Throw.DebugAssert( !string.IsNullOrWhiteSpace( actualTypeName  ) );
            nonNullableActualTypeNameIfNullable = null;
            if( isNullable )
            {
                nonNullableActualTypeNameIfNullable = actualTypeName;
                actualTypeName += "?";
            }
            var typeScope = ns.CreateType( $"sealed class {supportType.TypeName} : HashSet<{actualTypeName}>" );
            typeScope.Append( "public bool IsReadOnly => false;" ).NewLine();
            return typeScope;
        }


        static void AppendIReadOnlySetSupport( string actualTypeName,
                                               string? nonNullableActualTypeNameIfNullable,
                                               ITypeScope typeScope,
                                               bool isIPoco,
                                               IEnumerable<IPocoType> baseTypes )
        {
            bool isNullable = nonNullableActualTypeNameIfNullable != null;
            AppendReadOnly( typeScope, isNullable ? "object?" : "object", actualTypeName, nonNullableActualTypeNameIfNullable );
            if( isIPoco )
            {
                AppendReadOnly( typeScope, isNullable ? "IPoco?" : "IPoco", actualTypeName, nonNullableActualTypeNameIfNullable );
            }
            foreach( var a in baseTypes )
            {
                AppendReadOnly( typeScope, a.CSharpName, actualTypeName, nonNullableActualTypeNameIfNullable );
            }

            static void AppendReadOnly( ITypeScope typeScope, string abstractTypeName, string actualTypeName, string? nonNullableActualTypeNameIfNullable )
            {
                typeScope.Definition.BaseTypes.Add( new ExtendedTypeName( $"IReadOnlySet<{abstractTypeName}>" ) );

                AppendIReadOnlySetContains( typeScope, abstractTypeName, actualTypeName, nonNullableActualTypeNameIfNullable );

                typeScope.Append( "bool IReadOnlySet<" ).Append( abstractTypeName ).Append( ">.IsProperSubsetOf( IEnumerable<" ).Append( abstractTypeName ).Append( "> other ) => CovariantHelpers.IsProperSubsetOf( this, other );" ).NewLine()

                .Append( "bool IReadOnlySet<" ).Append( abstractTypeName ).Append( ">.IsProperSupersetOf( IEnumerable<" ).Append( abstractTypeName ).Append( "> other ) => CovariantHelpers.IsProperSupersetOf( this, other );" ).NewLine()

                .Append( "bool IReadOnlySet<" ).Append( abstractTypeName ).Append( ">.IsSubsetOf( IEnumerable<" ).Append( abstractTypeName ).Append( "> other ) => CovariantHelpers.IsSubsetOf( this, other );" ).NewLine()

                .Append( "bool IReadOnlySet<" ).Append( abstractTypeName ).Append( ">.IsSupersetOf( IEnumerable<" ).Append( abstractTypeName ).Append( "> other ) => CovariantHelpers.IsSupersetOf( this, other );" ).NewLine()

                .Append( "bool IReadOnlySet<" ).Append( abstractTypeName ).Append( ">.Overlaps( IEnumerable<" ).Append( abstractTypeName ).Append( "> other ) => CovariantHelpers.Overlaps( this, other );" ).NewLine()

                .Append( "bool IReadOnlySet<" ).Append( abstractTypeName ).Append( ">.SetEquals( IEnumerable<" ).Append( abstractTypeName ).Append( "> other ) => CovariantHelpers.SetEquals( this, other );" ).NewLine()

                .Append( "IEnumerator<" ).Append( abstractTypeName ).Append( "> IEnumerable<" ).Append( abstractTypeName ).Append( ">.GetEnumerator() => GetEnumerator();" ).NewLine();
            }
        }

        static void AppendIReadOnlySetContains( ITypeScope typeScope, string abstractTypeName, string actualTypeName, string? nonNullableActualTypeNameIfNullable )
        {
            typeScope.Append( "bool IReadOnlySet<" ).Append( abstractTypeName )
                .Append( ">.Contains( " ).Append( abstractTypeName ).Append( " item ) => " );
            if( nonNullableActualTypeNameIfNullable == null )
            {
                typeScope.Append( "item is " ).Append( actualTypeName ).Append( " v && Contains( v );" ).NewLine();
            }
            else
            {
                typeScope.Append( "(item is " ).Append( nonNullableActualTypeNameIfNullable ).Append( " v && Contains( v )) || (item == null && Contains( null ));" ).NewLine();
            }
        }

        static void AppendICollectionImpl( ITypeScope t, string abstractTypeName, string pocoClassName )
        {
            t.Append( "void ICollection<" ).Append( abstractTypeName ).Append( ">.Add( " ).Append( abstractTypeName )
               .Append( " item ) => Add( (" ).Append( pocoClassName ).Append( ")item );" ).NewLine()
            .Append( "bool ICollection<" ).Append( abstractTypeName ).Append( ">.Contains( " ).Append( abstractTypeName )
               .Append( " item ) => Contains( (" ).Append( pocoClassName ).Append( ")item );" ).NewLine()
            .Append( "void ICollection<" ).Append( abstractTypeName ).Append( ">.CopyTo( " )
               .Append( abstractTypeName ).Append( "[] array, int arrayIndex )" ).OpenBlock()
               .Append( "foreach( var e in this ) array[arrayIndex++] = e;" )
               .CloseBlock()
           .Append( "bool ICollection<" ).Append( abstractTypeName ).Append( ">.Remove( " ).Append( abstractTypeName )
               .Append( " item ) => Remove( (" ).Append( pocoClassName ).Append( ")item );" ).NewLine()
           .Append( "IEnumerator<" ).Append( abstractTypeName ).Append( "> IEnumerable<" ).Append( abstractTypeName ).Append( ">.GetEnumerator() => GetEnumerator();" )
           .NewLine();
        }

        static void GeneratePocoDictionary( IActivityMonitor monitor, INamespaceScope ns, IPocoDictionaryRequiredSupport dic )
        {
            var k = dic.KeyType.CSharpName;
            var actualTypeName = dic.ValueType.ImplTypeName;
            ITypeScope typeScope = CreateDictionaryType( ns, dic, k, actualTypeName );

            foreach( var tI in dic.ValueType.FamilyInfo.Interfaces )
            {
                AppendIReadOnlyDictionary( typeScope, k, tI.CSharpName, actualTypeName );

                typeScope.Definition.BaseTypes.Add( new ExtendedTypeName( $"IDictionary<{k},{tI.CSharpName}>" ) );

                typeScope.Append( "ICollection<" ).Append( k ).Append( "> IDictionary<" ).Append( k ).Append( ", " ).Append( tI.CSharpName ).Append( ">.Keys => Keys;" ).NewLine();
                typeScope.Append( "ICollection<" ).Append( tI.CSharpName ).Append( "> IDictionary<" ).Append( k ).Append( ", " ).Append( tI.CSharpName )
                    .Append( ">.Values => global::System.Runtime.CompilerServices.Unsafe.As<ICollection<" ).Append( tI.CSharpName ).Append( ">>( Values );" ).NewLine();
                typeScope.Append( tI.CSharpName ).Append( " IDictionary<" ).Append( k ).Append( ", " ).Append( tI.CSharpName )
                    .Append( ">.this[" ).Append( k ).Append( " key] { get => this[key]; set => this[key] = (" ).Append( actualTypeName ).Append( ")value; }" ).NewLine();
                typeScope.Append( "void IDictionary<" ).Append( k ).Append( ", " ).Append( tI.CSharpName ).Append( ">.Add( " ).Append( k ).Append( " key, " )
                    .Append( tI.CSharpName ).Append( " value ) => Add( key, (" ).Append( actualTypeName ).Append( ")value );" ).NewLine();
                typeScope.Append( "void ICollection<KeyValuePair<" ).Append( k ).Append( ", " ).Append( tI.CSharpName ).Append( ">>.Add( KeyValuePair<" ).Append( k ).Append( ", " ).Append( tI.CSharpName )
                    .Append( "> item ) => Add( item.Key, (" ).Append( actualTypeName ).Append( ")item.Value );" ).NewLine();

                typeScope.Append( "bool ICollection<KeyValuePair<" ).Append( k ).Append( ", " ).Append( tI.CSharpName ).Append( ">>.Contains( KeyValuePair<" ).Append( k ).Append( ", " ).Append( tI.CSharpName ).Append( "> item ) => base.TryGetValue( item.Key, out var v ) && v == item.Value;" ).NewLine();

                typeScope.Append( "void ICollection<KeyValuePair<" ).Append( k ).Append( ", " ).Append( tI.CSharpName ).Append( ">>.CopyTo( KeyValuePair<" ).Append( k ).Append( ", " ).Append( tI.CSharpName ).Append( ">[] array, int arrayIndex )" ).NewLine()
                    .Append( " => ((ICollection<KeyValuePair<" ).Append( k ).Append( ", " ).Append( actualTypeName ).Append( ">>)this).CopyTo( global::System.Runtime.CompilerServices.Unsafe.As<KeyValuePair<" ).Append( k ).Append( ", " ).Append( actualTypeName ).Append( ">[]>( array ), arrayIndex );" ).NewLine();

                typeScope.Append( "bool ICollection<KeyValuePair<" ).Append( k ).Append( ", " ).Append( tI.CSharpName ).Append( ">>.Remove( KeyValuePair<" ).Append( k ).Append( ", " ).Append( tI.CSharpName ).Append( "> item )" ).NewLine()
                    .Append( "=> ((ICollection<KeyValuePair<" ).Append( k ).Append( ", " ).Append( actualTypeName ).Append( ">>)this).Remove( new KeyValuePair<" ).Append( k ).Append( ", " ).Append( actualTypeName ).Append( ">( item.Key, (" ).Append( actualTypeName ).Append( ")item.Value ) );" ).NewLine();

            }

            AppendIReadOnlyDictionarySupport( actualTypeName,
                                              typeScope,
                                              k,
                                              isIPoco: true,
                                              dic.ValueType.AbstractTypes );
        }

        static void GeneratePocoDictionaryOfAbstractBasicRef( IActivityMonitor monitor, INamespaceScope ns, IPocoDictionaryOfAbstractOrBasicRefRequiredSupport dic )
        {
            var k = dic.KeyType.CSharpName;
            var actualTypeName = dic.ValueType.ImplTypeName;
            ITypeScope typeScope = CreateDictionaryType( ns, dic, k, actualTypeName );
            GetAllBaseTypes( dic.ValueType, out bool isIPoco, out IEnumerable<IPocoType> baseTypes );
            AppendIReadOnlyDictionarySupport( actualTypeName,
                                              typeScope,
                                              k,
                                              isIPoco,
                                              baseTypes );
        }

        static ITypeScope CreateDictionaryType( INamespaceScope ns, IPocoRequiredSupportType dic, string k, string actualTypeName )
        {
            var typeScope = ns.CreateType( $"sealed class {dic.TypeName} : Dictionary<{k},{actualTypeName}>" );
            typeScope.Append( "bool TGV<TOut>( " ).Append( k ).Append( " key, out TOut? value ) where TOut : class" ).NewLine()
                     .OpenBlock()
                     .Append( """
                            if( base.TryGetValue( key, out var v ) )
                            {
                                value = global::System.Runtime.CompilerServices.Unsafe.As<TOut>( v );
                                return true;
                            }
                            value = null;
                            return false;
                            """ )
                     .CloseBlock()
                     .Append( "public bool IsReadOnly => false;" ).NewLine();
            return typeScope;
        }

        static void AppendIReadOnlyDictionarySupport( string pocoClassName,
                                                      ITypeScope typeScope,
                                                      string k,
                                                      bool isIPoco,
                                                      IEnumerable<IPocoType> baseTypes )
        {
            AppendIReadOnlyDictionary( typeScope, k, "object", pocoClassName );
            if( isIPoco )
            {
                AppendIReadOnlyDictionary( typeScope, k, "IPoco", pocoClassName );
            }
            foreach( var a in baseTypes )
            {
                AppendIReadOnlyDictionary( typeScope, k, a.CSharpName, pocoClassName );
            }
        }

        static void AppendIReadOnlyDictionary( ITypeScope t, string k, string abstractTypeName, string actualTypeName )
        {
            t.Definition.BaseTypes.Add( new ExtendedTypeName( $"IReadOnlyDictionary<{k},{abstractTypeName}>" ) );
            t.Append( abstractTypeName ).Append( " IReadOnlyDictionary<" ).Append( k ).Append( ", " ).Append( abstractTypeName ).Append( ">.this[" ).Append( k ).Append( " key] => this[key];" ).NewLine();
            t.Append( "IEnumerable<" ).Append( k ).Append( "> IReadOnlyDictionary<" ).Append( k ).Append( ", " ).Append( abstractTypeName ).Append( ">.Keys => Keys;" ).NewLine();
            t.Append( "IEnumerable<" ).Append( abstractTypeName ).Append( "> IReadOnlyDictionary<" ).Append( k ).Append( ", " ).Append( abstractTypeName ).Append( ">.Values => Values;" ).NewLine();
            t.Append( "public bool TryGetValue( " ).Append( k ).Append( " key, out " ).Append( abstractTypeName ).Append( " value ) => TGV( key, out value );" ).NewLine();

            t.Append( "IEnumerator<KeyValuePair<" ).Append( k ).Append( ", " ).Append( abstractTypeName ).Append( ">> IEnumerable<KeyValuePair<" ).Append( k ).Append( "," ).Append( abstractTypeName ).Append( ">>.GetEnumerator()" ).NewLine()
             .Append( "=> ((IEnumerable<KeyValuePair<" ).Append( k ).Append( ", " ).Append( actualTypeName ).Append( ">>)this).Select( kv => KeyValuePair.Create( kv.Key, (" ).Append( abstractTypeName ).Append( ")kv.Value ) ).GetEnumerator();" ).NewLine();
        }

        CSCodeGenerationResult CheckNoMoreRegisteredPocoTypes( IActivityMonitor monitor )
        {
            var newCount = _typeSystemBuilder.Count;
            if( newCount != _lastRegistrationCount )
            {
                monitor.Trace( $"PocoTypeSystemBuilder has {newCount - _lastRegistrationCount} new types. Deferring Lock." );
                _lastRegistrationCount = newCount;
                return new CSCodeGenerationResult( nameof( CheckNoMoreRegisteredPocoTypes ) );
            }
            monitor.Info( $"PocoTypeSystemBuilder has no new types, code generation that requires the PocoTypeSystem can start." );
            IPocoTypeSystem ts = _typeSystemBuilder.Lock( monitor );

            _finalTypeIndex.Append( "static readonly Dictionary<Type,int> _finalTypes = new Dictionary<Type,int>( " ).Append( ts.NonNullableFinalTypes.Count ).Append( " ) {" ).NewLine();
            foreach( var type in ts.NonNullableFinalTypes )
            {
                _finalTypeIndex.Append( "{typeof(" ).Append( type.ImplTypeName ).Append( ")," ).Append( type.Index ).Append( "}," ).NewLine();
            }
            _finalTypeIndex.Append( "};" ).NewLine()
                           .Append( "public override int GetNonNullableFinalTypeIndex( Type t ) => _finalTypes.GetValueOrDefault( t, -1 );" ).NewLine();

            _context.CurrentRun.ServiceContainer.Add( ts );
            return CSCodeGenerationResult.Success;
        }
    }
}
