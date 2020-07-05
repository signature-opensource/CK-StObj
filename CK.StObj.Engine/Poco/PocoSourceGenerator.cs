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

        public override AutoImplementationResult Implement( IActivityMonitor monitor, Type classType, ICodeGenerationContext c, ITypeScope scope )
        {
            Debug.Assert( scope.FullName == "CK.Core.PocoDirectory_CK", "We can use the PocoDirectory_CK type name to reference the PocoDirectory implementation." );

            IPocoSupportResult r = c.Assembly.GetPocoSupportResult();

            // Poco class.
            scope.Append( "Dictionary<string,IPocoFactory> _factories = new Dictionary<string,IPocoFactory>( " ).Append( r.NamedRoots.Count ).Append( " );" )
                 .NewLine()
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
                tFB.TypeDefinition.Modifiers |= Modifiers.Sealed;
                string factoryClassName = tFB.TypeDefinition.Name.Name;

                // Poco class.
                var tB = c.Assembly.FindOrCreateAutoImplementedClass( monitor, root.PocoClass );
                tB.TypeDefinition.Modifiers |= Modifiers.Sealed;

                // The factory field is private and its type is the exact class: extended code can refer to the _factory
                // and can have access to the factory extended code without cast.
                tB.Append( "readonly " ).Append( factoryClassName ).Append( " _factory;" ).NewLine();
                tB.Append( "public IPocoFactory Factory => _factory;" ).NewLine();
                
                // Always create the constructor so that other code generators
                // can always find it.
                // We support the interfaces here: if other participants have already created this type, it is
                // up to us, here, to handle the "exact" type definition.
                tB.TypeDefinition.BaseTypes.Add( new ExtendedTypeName( "IPocoClass" ) );
                tB.TypeDefinition.BaseTypes.AddRange( root.Interfaces.Select( i => new ExtendedTypeName( i.PocoInterface.ToCSharpName() ) ) );

                IFunctionScope ctorB = tB.CreateFunction( $"public {root.PocoClass.Name}( PocoDirectory_CK d )" );
                ctorB.Append( "_factory = d._f" ).Append( tFB.UniqueId ).Append( ';' );

                foreach( var p in root.PropertyList )
                {
                    Type propType = p.PropertyType;
                    tB.Append( "public " ).AppendCSharpName( propType ).Space().Append( p.PropertyName ).Append( "{get;" );
                    // We always implement a setter except if we are auto instantiating the value and NO properties are writable.
                    if( !p.AutoInstantiated || p.HasDeclaredSetter ) tB.Append( "set;" );
                    tB.Append( "}" );
                    Debug.Assert( !p.AutoInstantiated || p.DefaultValueSource == null, "AutoInstantiated with [DefaultValue] has already raised an error." );

                    if( p.AutoInstantiated )
                    {
                        ctorB.Append( p.PropertyName ).Append( " = " );
                        r.WriteAutoInstantiatedNewObject( ctorB, propType, "d" );
                    }
                    if( p.DefaultValueSource != null )
                    {
                        tB.Append( " = " ).Append( p.DefaultValueSource ).Append( ";" );
                    }
                    tB.NewLine();
                }

                // PocoFactory class.

                // The PocoDirectory field is set by the StObjCostruct below. It is public and is 
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
                    tFB.TypeDefinition.BaseTypes.Add( new ExtendedTypeName( i.PocoFactoryInterface.ToCSharpName() ) );
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
