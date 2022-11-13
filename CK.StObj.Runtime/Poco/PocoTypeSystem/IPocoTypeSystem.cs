using CK.Core;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// The Poco type system manages by default all the types reachable from IPoco
    /// objects. Further <see cref="IRecordPocoType"/> and <see cref="ICollectionPocoType"/> can
    /// be registered.
    /// </summary>
    public interface IPocoTypeSystem
    {
        /// <summary>
        /// Gets all the registered types by their <see cref="IPocoType.Index"/>.
        /// This contains both nullable and non nullable types.
        /// </summary>
        IReadOnlyList<IPocoType> AllTypes { get; }

        /// <summary>
        /// Gets all the registered non nullable types.
        /// </summary>
        IReadOnlyList<IPocoType> AllNonNullableTypes { get; }

        /// <summary>
        /// Gets the set of types that must be generated to support this type system.
        /// </summary>
        IReadOnlyCollection<PocoRequiredSupportType> RequiredSupportTypes { get; }

        /// <summary>
        /// Tries to find a Poco type from an actual type.
        /// Anonymous <see cref="IRecordPocoType"/> cannot be found by this method.
        /// When the <paramref name="type"/> is a reference type, its non nullable
        /// Poco type is returned.
        /// </summary>
        /// <param name="type">The type to find.</param>
        /// <returns>The Poco type or null.</returns>
        IPocoType? FindByType( Type type );
        
        /// <summary>
        /// Gets the primary poco type from any of its interface.
        /// </summary>
        /// <param name="i">The IPoco interface.</param>
        /// <returns>The primary poco type or null.</returns>
        IPrimaryPocoType? GetPrimaryPocoType( Type i );

        /// <summary>
        /// Captures a type registration result.
        /// </summary>
        public readonly struct RegisterResult
        {
            readonly string? _regCSharpName;

            /// <summary>
            /// The registered poco type.
            /// </summary>
            public readonly IPocoType PocoType;

            /// <summary>
            /// Gets the registered type name.
            /// When <see cref="HasRegCSharpName"/> is true, this name differs from the <see cref="IPocoType.CSharpName"/>
            /// because the actual Poco type is an implementation type: <c>IList&lt;&gt;</c> of non nullable value types for
            /// instance are mapped to <see cref="CovariantHelpers.CovNotNullValueList{T}"/>.
            /// </summary>
            public string RegCSharpName => _regCSharpName ?? PocoType.CSharpName;

            /// <summary>
            /// Gets whether the registered type name is not the same as <see cref="IPocoType.CSharpName"/>.
            /// </summary>
            public bool HasRegCSharpName => _regCSharpName != null;

            /// <summary>
            /// Initializes a new registration result.
            /// </summary>
            /// <param name="pocoType">The resulting Poco type.</param>
            /// <param name="registeredTypeName">The registered type name if it differs from the <see cref="IPocoType.CSharpName"/>.</param>
            public RegisterResult( IPocoType pocoType, string? registeredTypeName )
            {
                Throw.CheckNotNullArgument( pocoType );
                Throw.CheckArgument( registeredTypeName == null || pocoType.IsNullable == (registeredTypeName[registeredTypeName.Length-1] == '?') );
                PocoType = pocoType;
                _regCSharpName = registeredTypeName;
            }

            /// <summary>
            /// Gets the nullable associated registration.
            /// </summary>
            public RegisterResult Nullable => new RegisterResult( PocoType.Nullable, HasRegCSharpName ? RegCSharpName + "?" : null );

            /// <summary>
            /// Gets the non nullable associated registration.
            /// </summary>
            public RegisterResult NonNullable => new RegisterResult( PocoType.NonNullable,
                                                                     _regCSharpName != null ? _regCSharpName.Substring( 0, _regCSharpName.Length - 1 ) : null );
        }

        /// <summary>
        /// Tries to register a type.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="p">The <see cref="IExtMemberInfo"/> whose <see cref="IExtMemberInfo.Type"/> must be registered.</param>
        /// <returns>The poco type on success, null otherwise.</returns>
        RegisterResult? Register( IActivityMonitor monitor, IExtMemberInfo memberInfo );

        /// <summary>
        /// Tries to register a new type through a PropertyInfo (this is required for
        /// nullability analysis).
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="p">The PropertyInfo whose <see cref="PropertyInfo.PropertyType"/> must be registered.</param>
        /// <returns>The poco type on success, null otherwise.</returns>
        RegisterResult? Register( IActivityMonitor monitor, PropertyInfo p );

        /// <summary>
        /// Tries to register a new type through a FieldInfo (this is required for
        /// nullability analysis).
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="f">The FieldInfo whose <see cref="FieldInfo.FieldType"/> must be registered.</param>
        /// <returns>The poco type on success, null otherwise.</returns>
        RegisterResult? Register( IActivityMonitor monitor, FieldInfo f );

        /// <summary>
        /// Tries to register a new type through a ParameterInfo (this is required for
        /// nullability analysis).
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="f">The ParameterInfo whose <see cref="ParameterInfo.ParameterType"/> must be registered.</param>
        /// <returns>The poco type on success, null otherwise.</returns>
        RegisterResult? Register( IActivityMonitor monitor, ParameterInfo f );

    }

}
