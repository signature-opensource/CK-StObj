using CK.Core;
using System.Reflection;
using System;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Primitives;
using System.Linq;

namespace CK.Engine.TypeCollector;

partial class CachedType
{
    internal static TypeKind ComputeUnhandledTypeKind( Type t, Assembly? assembly )
    {
        if( t.FullName == null ) return TypeKind.IsNullFullName;
        if( (assembly ?? t.Assembly).IsDynamic ) return TypeKind.IsFromDynamicAssembly;
        if( !t.IsVisible ) return TypeKind.IsNotVisible;
        if( !t.IsValueType || !t.IsClass || !t.IsInterface || !t.IsEnum ) return TypeKind.IsNotClassEnumValueTypeOrEnum;
        return TypeKind.None;
    }

    TypeKind ComputeTypeKind( Type t )
    {
        TypeKind k = ComputeUnhandledTypeKind( t, _assembly.Assembly );
        if( k == TypeKind.None
            && t != typeof( object )
            && t != typeof( ValueType )
            && (t.IsClass || t.IsInterface) )
        {
            if( _isGenericType && !_isGenericTypeDefinition )
            {
                Throw.DebugAssert( GenericTypeDefinition != null );
                // A Generic Type definition can be a (Super)Definer or be a multiple service definition: this
                // applies directly to the closed generic type.
                k = GenericTypeDefinition.Kind;
            }
            else
            {
                Throw.DebugAssert( typeof( Core.ExcludeCKTypeAttribute ).Name == "ExcludeCKTypeAttribute" );
                Throw.DebugAssert( typeof( ContainerConfiguredScopedServiceAttribute ).Name == "ContainerConfiguredScopedServiceAttribute" );
                Throw.DebugAssert( typeof( ContainerConfiguredSingletonServiceAttribute ).Name == "ContainerConfiguredSingletonServiceAttribute" );
                Throw.DebugAssert( typeof( CKTypeSuperDefinerAttribute ).Name == "CKTypeSuperDefinerAttribute" );
                Throw.DebugAssert( typeof( CKTypeDefinerAttribute ).Name == "CKTypeDefinerAttribute" );
                Throw.DebugAssert( typeof( IsMultipleAttribute ).Name == "IsMultipleAttribute" );
                bool hasSuperDefiner = false;
                bool hasDefiner = false;
                bool isMultiple = false;
                bool isExcludedType = false;
                bool hasContainerConfiguredScoped = false;
                bool hasAmbientService = false;
                bool hasContainerConfiguredSingleton = false;

                // Now process the attributes of the type. This sets the variables above
                // but doesn't touch k except to set it to ExcudedType if a [StObjGen] is
                // found on the type. 
                int intrinsicAttrCount = 0;
                foreach( var a in CustomAttributes )
                {
                    var n = a.AttributeType.Name;
                    if( n == "StObjGenAttribute" )
                    {
                        // This attributes stops all subsequent analysis (it's the only one).
                        // A [StObjGen] is necessarily None.
                        k = TypeKind.IsIntrinsicExcluded;
                        ActivityMonitor.StaticLogger.Trace( $"Type '{_csharpName}' is [StObjGen]. It is ignored." );
                        break;
                    }
                    switch( n )
                    {
                        case "CKTypeDefinerAttribute":
                            hasDefiner = true;
                            intrinsicAttrCount++;
                            break;
                        case "CKTypeSuperDefinerAttribute":
                            hasSuperDefiner = true;
                            intrinsicAttrCount++;
                            break;
                        case "IsMultipleAttribute":
                            isMultiple = true;
                            intrinsicAttrCount++;
                            break;
                        case "ExcludeCKTypeAttribute":
                            isExcludedType = true;
                            intrinsicAttrCount++;
                            break;
                        case "ScopedContainerConfiguredServiceAttribute":
case "ContainerConfiguredScopedServiceAttribute":
                            hasContainerConfiguredScoped = true;
                            hasAmbientService = a.ConstructorArguments.Count == 1 && a.ConstructorArguments[0].Value is bool b && b;
                            intrinsicAttrCount++;
                            break;
                        case "SingletonContainerConfiguredServiceAttribute":
case "ContainerConfiguredSingletonServiceAttribute":
                            hasContainerConfiguredSingleton = true;
                            intrinsicAttrCount++;
                            break;
                    }
                }
                Throw.DebugAssert( k == TypeKind.None || k == TypeKind.IsIntrinsicExcluded );
                if( k == TypeKind.None )
                {
                    // First, handle intrinsic incoherencies.
                    if( isExcludedType && intrinsicAttrCount > 1 )
                    {
                        ThrowTypeError( $"has [ExcludeCKType] attrirute. It cannot also have [{GetIntrinsicAttributeNames( false ).Concatenate("], [")}] attributes." );       
                    }
                    if( hasContainerConfiguredScoped && hasContainerConfiguredSingleton )
                    {
                        ThrowTypeError( $"cannot have both [ScopedContainerConfiguredService] and [SingletonContainerConfiguredService] attributes." );
                    }
                    // Normalizes SuperDefiner => Definer (and emits a warning).
                    if( hasSuperDefiner )
                    {
                        if( hasDefiner )
                        {
                            ActivityMonitor.StaticLogger.Warn( $"Attribute [CKTypeDefiner] defined on type '{_csharpName}' is useless since [CKTypeSuperDefiner] is also defined." );
                        }
                        hasDefiner = true;
                    }
                    // Type's attributes have been analyzed, IsDefiner is normalized.
                    // It's time to apply the bases.
                    foreach( var i in DeclaredBaseTypes )
                    {
                        var kI = i.Kind & ~(TypeKind.IsDefiner | TypeKind.IsMultipleService | TypeKind.IsIntrinsicExcluded);
                        if( (kI & TypeKind.IsSuperDefiner) != 0 ) // This base is a SuperDefiner.
                        {
                            kI |= TypeKind.IsDefiner;
                            kI &= ~TypeKind.IsSuperDefiner;
                        }
                        k |= kI;
                    }
                    // Consider the 3 fundamental types.
                    // Check that the a IPoco type is not also a IRealObject or IAutoService
                    // has already been done by the GlobalTypeCache.
                    bool isAutoService = (k & TypeKind.IsAutoService) != 0;
                    bool isPoco = (k & TypeKind.IsPoco) != 0;
                    bool isRealObject = (k & TypeKind.IsRealObject) != 0;
                    string? rpsType = isRealObject
                                        ? "IRealObject"
                                        : isPoco
                                        ? "IPoco"
                                        : isAutoService
                                        ? "IAutoService"
                                        : null;
                    // [ExcludeCKType] on IPoco is forbidden.
                    // [Scoped or SingletonContainerConfiguredService] on IRealObject, IPoco or IAutoService are forbidden.
                    if( rpsType != null && (hasContainerConfiguredScoped || hasContainerConfiguredSingleton || (isExcludedType && isPoco)) )
                    {
                        ThrowTypeError( $"[{(hasContainerConfiguredSingleton
                                                ? "SingletonContainerConfiguredService"
                                                : hasContainerConfiguredScoped
                                                    ? "ScopedContainerConfiguredService"
                                                    : "ExcludeCKType")}] are invalid because it is a {rpsType}." );
                    }
                    // Applying the intrinsics.
                    // First handles the IsMultiple.
                    if( isMultiple )
                    {
                        if( isPoco )
                        {
                            ThrowTypeError( $"is a IPoco and cannot be have [IsMultiple] attribute. Use [CKTypeDefiner] instead." );
                        }
                        Throw.DebugAssert( t.IsClass || t.IsInterface );
                        if( t.IsClass )
                        {
                            // A static class IsAbstract and IsSealed in .Net.
                            if( !t.IsAbstract )
                            {
                                ThrowTypeError( $"is a non abstract class. Only interfaces and abstract classes can have [IsMultiple] attribute." );
                            }
                            if( t.IsSealed )
                            {
                                ThrowTypeError( $"is a static class. It cannot have [IsMultiple] attribute." );
                            }
                        }
                        else
                        {
                            if( isRealObject )
                            {
                                ThrowTypeError( $"is a IRealObject interface. [IsMultiple] attribute can only be applied to a IRealObject abstract class." );
                            }
                        }
                        if( (isRealObject || isAutoService) && !hasDefiner )
                        {
                            ActivityMonitor.StaticLogger.Warn( $"Type '{_csharpName}' is a [IsMultiple] {rpsType}. It should also be decorated with [CKTypeDefiner] or [CKTypeSuperDefiner]." );
                            hasDefiner = true;
                        }
                        k |= TypeKind.IsMultipleService;
                    }

                    if( isExcludedType ) k |= TypeKind.IsIntrinsicExcluded;
                    if( hasSuperDefiner ) k |= TypeKind.IsSuperDefiner;
                    if( hasDefiner ) k |= TypeKind.IsDefiner;
                    if( hasContainerConfiguredSingleton ) k |= TypeKind.IsContainerConfiguredService | TypeKind.IsSingleton;
                    if( hasAmbientService ) k |= TypeKind.IsAmbientService | TypeKind.IsContainerConfiguredService | TypeKind.IsScoped;
                    else if( hasContainerConfiguredScoped ) k |= TypeKind.IsContainerConfiguredService | TypeKind.IsScoped;

                    if( isPoco )
                    {
                        if( !t.IsInterface )
                        {
                            ThrowTypeError( $"a IPoco can only be an interface." );
                        }
                        if( k.WithoutDefiners() != TypeKind.IsPoco )
                        {
                            ThrowTypeError( $"IPoco cannot be combined with any aspect other than [CKTypeDefiner] or [CKTypeSuperDefiner]." );
                        }
                    }
                    else 
                    {
                        bool isScoped = (k & TypeKind.IsScoped) != 0;
                        bool isSingleton = (k & TypeKind.IsSingleton) != 0;
                        if( isScoped && isSingleton )
                        {
                            ThrowTypeError( $"lifetime cannot be both Singleton and Scoped." );
                        }
                        if( isRealObject && isAutoService && !t.IsClass )
                        {
                            ThrowTypeError( $"IRealObject interface cannot be a IAutoService." );
                        }
                        bool isAmbientService = (k & TypeKind.IsAmbientService) != 0;
                        if( isAmbientService )
                        {
                            var kAllowed = k.WithoutIntrinsicRegister() & ~TypeKind.IsAutoService;
                            if( k != kAllowed )
                            {
                                ThrowTypeError( "an ambient service can only be Scoped and configured by container." );
                            }
                        }
                    }
                }
            }
        }

        return k;
    }

    void ThrowTypeError( string message )
    {
        var b = new StringBuilder( "Invalid: '" ).Append( _csharpName ).Append( "':" ).AppendLine( message ).Append( " Type details:" );
        foreach( var t in DeclaredBaseTypes.Reverse() )
        {
            DumpType( b, t );
        }
        DumpType( b, this );
        Throw.CKException( b.ToString() );

        static void DumpType( StringBuilder b, ICachedType t )
        {
            b.AppendLine();
            int idx = 0;
            foreach( var a in ((CachedType)t).GetIntrinsicAttributeNames( true ) )
            {
                if( idx == 0 ) b.Append( '[' );
                else b.Append( ", " );
                b.Append( a );
                ++idx;
            }
            if( idx > 0 ) b.Append( ']' ).AppendLine();
            b.Append( t.CSharpName );
            idx = 0;
            foreach( var t2 in t.DeclaredBaseTypes )
            {
                b.Append( idx == 0 ? ':' : ',' ).Append( ' ' ).Append( t2.CSharpName );
            }
        }
    }

    IEnumerable<string> GetIntrinsicAttributeNames( bool withExcludeCKType )
    {
        foreach( var a in CustomAttributes )
        {
            switch( a.AttributeType.Name )
            {
                case "CKTypeDefinerAttribute":
                    yield return "CKTypeDefiner";
                    break;
                case "CKTypeSuperDefinerAttribute":
                    yield return "CKTypeSuperDefiner";
                    break;
                case "IsMultipleAttribute":
                    yield return "IsMultiple";
                    break;
                case "ExcludeCKTypeAttribute":
                    if( withExcludeCKType ) yield return "ExcludeCKType";
                    break;
                case "ScopedContainerConfiguredServiceAttribute":
case "ContainerConfiguredScopedServiceAttribute":
                    yield return "ScopedContainerConfiguredService";
                    break;
                case "SingletonContainerConfiguredServiceAttribute":
case "ContainerConfiguredSingletonServiceAttribute":
                    yield return "SingletonContainerConfigured";
                    break;
            }
        }
    }

}
