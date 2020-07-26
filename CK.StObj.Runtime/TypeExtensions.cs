using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CK.Setup
{
    /// <summary>
    /// Extends Type with helper methods.
    /// </summary>
    public static class TypeExtensions
    {
        /// <summary>
        /// Gets the <see cref="ExternalNameAttribute"/> names or this <see cref="Type.FullName"/> (and
        /// emits a warning if the full name is used).
        /// </summary>
        /// <param name="t">This type.</param>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="name">The name to use to identify the type.</param>
        /// <param name="previousNames">Optional previous names.</param>
        /// <returns>True on success, false on error.</returns>
        public static bool GetExternalNames( this Type t, IActivityMonitor monitor, out string name, out string[] previousNames )
        {
            var names = t.GetCustomAttributesData().Where( d => typeof( ExternalNameAttribute ).IsAssignableFrom( d.AttributeType ) ).FirstOrDefault();

            if( names != null )
            {
                var args = names.ConstructorArguments;
                name = (string)args[0].Value!;
                previousNames = ((IEnumerable<CustomAttributeTypedArgument>)args[1].Value!).Select( a => (string)a.Value! ).ToArray();
                if( String.IsNullOrWhiteSpace( name ) )
                {
                    monitor.Error( $"Empty name in ExternalName attribute on '{t.FullName}'." );
                    return false;
                }
                if( previousNames.Any( n => String.IsNullOrWhiteSpace( n ) ) )
                {
                    monitor.Error( $"Empty previous name in ExternalName attribute on '{t.FullName}'." );
                    return false;
                }
                if( previousNames.Contains( name ) || previousNames.GroupBy( Util.FuncIdentity ).Any( g => g.Count() > 1 ) )
                {
                    monitor.Error( $"Duplicate ExternalName in attribute on '{t.FullName}'." );
                    return false;
                }
            }
            else
            {
                name = t.FullName!;
                previousNames = Array.Empty<string>();
                monitor.Warn( $"Type '{name}' use its full name as its name since no [ExternalName] attribute is defined." );
            }
            return true;
        }

        /// <summary>
        /// Gets whether this type is a <see cref="ValueTuple"/>.
        /// </summary>
        /// <param name="this"></param>
        /// <returns></returns>
        public static bool IsValueTuple( this Type @this )
        {
            return @this.Namespace == "System" && @this.Name.StartsWith( "ValueTuple`" );
        }

        /// <summary>
        /// Gets the <see cref="NullabilityTypeKind"/> for a type.
        /// A reference type cannot be, by itself, a <see cref="NullabilityTypeKind.NonNullableReferenceType"/>: this
        /// method always return <see cref="NullabilityTypeKind.NullableReferenceType"/> or <see cref="NullabilityTypeKind.NullableGenericReferenceType"/> for
        /// classes and interfaces and always <see cref="NullabilityTypeKind.NullableArrayType"/> for arrays.
        /// </summary>
        /// <para>
        /// <c>typeof(List&lt;string?&gt;)</c> is valid and type but <c>typeof(List&lt;string?&gt;?)</c>
        /// cannot compile and this makes sense: the "outer", "root" nullability depends on the usage of the type: non nullable reference types can be obtained
        /// via a <see cref="ParameterInfo"/> or a <see cref="PropertyInfo"/> that "references" their type.
        /// However, <c>typeof(List&lt;string?&gt;)</c> could have been a <see cref="NullabilityTypeKind.NRTFullNullable"/>, but it is not, it is actually
        /// oblivious to nullable: both <c>typeof(List&lt;string?&gt;)</c> and <c>typeof(List&lt;string&gt;)</c> are marked with with a single 0 byte.
        /// </para>
        /// <param name="this">This type.</param>
        /// <returns></returns>
        public static NullabilityTypeKind GetNullabilityKind( this Type @this )
        {
            if( @this.IsInterface )
            {
                return @this.IsGenericType ? NullabilityTypeKind.NullableGenericReferenceType : NullabilityTypeKind.NullableReferenceType;
            }
            if( @this.IsClass )
            {
                if( @this.IsGenericType ) return NullabilityTypeKind.NullableGenericReferenceType;
                return @this.IsArray ? NullabilityTypeKind.NullableArrayType : NullabilityTypeKind.NullableReferenceType;
            }
            if( @this.IsValueType )
            {
                if( @this.IsGenericType && @this.GetGenericTypeDefinition() == typeof( Nullable<> ) ) return NullabilityTypeKind.NullableValueType;
                return NullabilityTypeKind.NonNullableValueType;
            }
            throw new Exception( $"What's this type that is not an interface, a class or a value type?: {@this.AssemblyQualifiedName}" );
        }

        /// <summary>
        /// Gets the <see cref="NullabilityTypeInfo"/> for a parameter.
        /// <param name="this">This parameter.</param>
        /// <returns>The nullability info for the parameter.</returns>
        public static NullabilityTypeInfo GetNullabilityInfo( this ParameterInfo @this )
        {
            return GetNullabilityInfo( @this.ParameterType, @this.Member, @this.GetCustomAttributesData(), () => $" parameter '{@this.Name}' of {@this.Member.DeclaringType}.{@this.Member.Name}." );
        }

        /// <summary>
        /// Gets the <see cref="NullabilityTypeInfo"/> for a property.
        /// <param name="this">This parameter.</param>
        /// <returns>The nullability info for the parameter.</returns>
        public static NullabilityTypeInfo GetNullabilityInfo( this PropertyInfo @this )
        {
            return GetNullabilityInfo( @this.PropertyType, @this.DeclaringType, @this.GetCustomAttributesData(), () => $" property '{@this.Name}' of {@this.DeclaringType}." );
        }

        /// <summary>
        /// Gets the <see cref="NullabilityTypeInfo"/> for a field.
        /// <param name="this">This parameter.</param>
        /// <returns>The nullability info for the field.</returns>
        public static NullabilityTypeInfo GetNullabilityInfo( this FieldInfo @this )
        {
            return GetNullabilityInfo( @this.FieldType, @this.DeclaringType, @this.GetCustomAttributesData(), () => $" field '{@this.Name}' of {@this.DeclaringType}." );
        }

        static NullabilityTypeInfo GetNullabilityInfo( Type t, MemberInfo? parent, IEnumerable<CustomAttributeData> attributes, Func<string> locationForError )
        {
            bool[]? profile = null;
            var n = GetNullabilityKind( t );
            if( n.IsArrayOrReferenceType() )
            {
                Debug.Assert( (n & NullabilityTypeKind.IsNullable) != 0 );
                var a = attributes.FirstOrDefault( a => a.AttributeType.Name == "NullableAttribute" && a.AttributeType.Namespace == "System.Runtime.CompilerServices" );
                if( a == null )
                {
                    while( parent != null )
                    {
                        a = parent.GetCustomAttributesData().FirstOrDefault( a => a.AttributeType.Name == "NullableContextAttribute" && a.AttributeType.Namespace == "System.Runtime.CompilerServices" );
                        if( a != null )
                        {
                            n = HandleByte( locationForError, n, (byte)a.ConstructorArguments[0].Value! );
                            break;
                        }
                        parent = parent.DeclaringType;
                    }
                }
                else
                {
                    object? data = a.ConstructorArguments[0].Value;
                    // A single value means "apply to everything in the type", e.g. 1 for Dictionary<string, string>, 2 for Dictionary<string?, string?>?
                    if( data is byte b )
                    {
                        n = HandleByte( locationForError, n, b );
                    }
                    else if( data is IEnumerable<CustomAttributeTypedArgument> arguments )
                    {
                        profile = arguments.Select( v => (byte)v.Value! switch
                        {
                            1 => false,
                            2 => true,
                            _ => throw new Exception( $"Invalid byte value in NullableAttribute(...) for {locationForError()}." )
                        }  ).ToArray();
                        if( profile.Length == 0 )
                        {
                            throw new Exception( $"Missing byte values in NullableAttribute(...) for {locationForError()}." );
                        }
                        n |= NullabilityTypeKind.NRTFullNullable | NullabilityTypeKind.NRTFullNonNullable;
                        if( !profile[0] ) n &= ~NullabilityTypeKind.IsNullable;
                    }
                    else
                    {
                        throw new Exception( $"Invalid data type '{data?.GetType()}' in NullableAttribute for {locationForError()}." );
                    }
                }
            }
            return new NullabilityTypeInfo( n, profile );

            static NullabilityTypeKind HandleByte( Func<string> locationForError, NullabilityTypeKind n, byte b )
            {
                if( b == 1 )
                {
                    n &= ~NullabilityTypeKind.IsNullable;
                    n |= NullabilityTypeKind.NRTFullNonNullable;
                }
                else if( b == 2 )
                {
                    n |= NullabilityTypeKind.NRTFullNullable;
                }
                else
                {
                    throw new Exception( $"Invalid byte value in NullableAttribute({b}) for {locationForError()}." );
                }

                return n;
            }
        }


    }
}
