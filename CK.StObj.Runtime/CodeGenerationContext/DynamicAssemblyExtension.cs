#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Engine\AutoImplementor\DynamicAssembly.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using CK.CodeGen;
using CK.Core;
using System;

namespace CK.Setup
{
    /// <summary>
    /// Extends <see cref="IDynamicAssembly"/>.
    /// </summary>
    public static class DynamicAssemblyExtension
    {
        /// <summary>
        /// Gets a type name in the same namespace as the provided type.
        /// This method is idempotent, it simply ensures that the returned name ends with "_CK":
        /// it can safely be called on the generated Stub type itself.
        /// </summary>
        /// <param name="this">This Dynamic assembly.</param>
        /// <param name="type">The base or stub type.</param>
        /// <returns>The stub or generated type name.</returns>
        public static string GetAutoImplementedTypeName( this IDynamicAssembly @this, Type type )
        {
            var n = type.FullName;
            if( String.IsNullOrEmpty( n ) ) throw new ArgumentException( $"Type '{type}' doesn't have a FullName." );
            n = n.Replace( '+', '_' );
            return n.EndsWith( "_CK", StringComparison.Ordinal ) ? n : n + "_CK";
        }

        /// <summary>
        /// Gets or creates the <see cref="ITypeScope"/> builder from an interface, the direct base type or from
        /// an already stub type that is named with <see cref="GetAutoImplementedTypeName(IDynamicAssembly, Type)"/> (its
        /// name ends with "_CK").
        /// <para>
        /// Public constructors of the <see cref="Type.BaseType"/> if it exists are automatically replicated: protected
        /// constructors are to be called by generated code if needed. 
        /// </para>
        /// </summary>
        /// <param name="this">This Dynamic assembly.</param>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="type">The base or stub type. Can be an interface.</param>
        /// <returns>Th generated class builder.</returns>
        public static ITypeScope FindOrCreateAutoImplementedClass( this IDynamicAssembly @this, IActivityMonitor monitor, Type type ) => FindOrCreateAutoImplementedClass( @this, monitor, type, out bool _ );

        /// <summary>
        /// Gets or creates the <see cref="ITypeScope"/> builder from an interface, the direct base type or from
        /// an already stub type that is named with <see cref="GetAutoImplementedTypeName(IDynamicAssembly, Type)"/> (its
        /// name ends with "_CK").
        /// <para>
        /// Public constructors of the <see cref="Type.BaseType"/> if it exists are automatically replicated: protected
        /// constructors are to be called by generated code if needed. 
        /// </para>
        /// </summary>
        /// <param name="this">This Dynamic assembly.</param>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="type">The base or stub type. Can be an interface.</param>
        /// <param name="created">True if the type scope has been created. False if it was already defined.</param>
        /// <returns>Th generated class builder.</returns>
        public static ITypeScope FindOrCreateAutoImplementedClass( this IDynamicAssembly @this, IActivityMonitor monitor, Type type, out bool created )
        {
            if( type == null ) throw new ArgumentNullException( nameof( type ) );
            Type? baseType;
            string name = type.Name;
            if( name.EndsWith( "_CK", StringComparison.Ordinal ) )
            {
                baseType = type.BaseType;
            }
            else
            {
                baseType = type;
                name += "_CK";
            }
            var ns = @this.Code.Global.FindOrCreateNamespace( type.Namespace ?? String.Empty );

            ITypeScope? tB = ns.FindType( name );
            if( created = (tB == null) )
            {
                monitor.Trace( $"Creating ITypeScope builder for class: '{ns.FullName}.{name}'." );
                tB = ns.CreateType( "internal class "+ name );
                if( baseType != null )
                {
                    if( baseType != typeof( object ) )
                    {
                        tB.Definition.BaseTypes.Add( new ExtendedTypeName( baseType.ToCSharpName() ) );
                        // Only public constructors are replicated: protected constructors are to be called
                        // by generated code. 
                        tB.CreatePassThroughConstructors( baseType, ctor => ctor.IsPublic ? "public " : null );
                    }
                }
                else if( type.IsInterface )
                {
                    tB.Definition.BaseTypes.Add( new ExtendedTypeName( type.ToCSharpName() ) );
                }
            }
            return tB!;
        }

        /// <summary>
        /// Gets all information related to Poco support.
        /// This is never null: if an error occurred, <see cref="EmptyPocoSupportResult.Default"/> is used.
        /// Note that if no error but no Poco have been found, an empty result is produced that will not be
        /// the <see cref="EmptyPocoSupportResult.Default"/> instance.
        /// </summary>
        /// <param name="this">This Dynamic assembly.</param>
        /// <returns>The Poco information.</returns>
        public static IPocoSupportResult GetPocoSupportResult( this IDynamicAssembly @this ) => (IPocoSupportResult)@this.Memory[typeof( IPocoSupportResult )]!;

    }
}
