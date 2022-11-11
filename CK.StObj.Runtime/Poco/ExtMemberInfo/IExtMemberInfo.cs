using CK.Core;
using CK.Core.Impl;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;

namespace CK.Setup
{
    /// <summary>
    /// Unifies parameters, properties, fields, events and caches the
    /// custom attributes.
    /// </summary>
    public interface IExtMemberInfo
    {
        /// <summary>
        /// Gets the parameter info if this is a parameter or null.
        /// </summary>
        IExtParameterInfo? AsParameterInfo { get; }

        /// <summary>
        /// Gets the property info if this is a property or null.
        /// </summary>
        IExtPropertyInfo? AsPropertyInfo { get; }

        /// <summary>
        /// Gets the field info if this is a field or null.
        /// </summary>
        IExtFieldInfo? AsFieldInfo { get; }

        /// <summary>
        /// Gets the event info if this is an event or null.
        /// </summary>
        IExtEventInfo? AsEventInfo { get; }

        /// <summary>
        /// Gets the Type that declares this member.
        /// For <see cref="IExtParameterInfo"/> this is the type that declares the method.
        /// </summary>
        Type DeclaringType { get; }

        /// <summary>
        /// Gets the Type of the member.
        /// </summary>
        Type Type { get; }

        /// <summary>
        /// Gets the member name.
        /// This is the empty string for a returned <see cref="ParameterInfo"/> by a method.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the member type C# name. This caches <see cref="CK.Core.TypeExtensions.ToCSharpName(Type?, bool, bool, bool)"/>,
        /// there is no handling of Nullable Reference Type here.
        /// </summary>
        string TypeCSharpName { get; }

        /// <summary>
        /// Gets the custom attributes data of this member.
        /// </summary>
        IReadOnlyList<CustomAttributeData> CustomAttributesData { get; }

        /// <summary>
        /// Gets the custom attributes of this member.
        /// </summary>
        IReadOnlyList<object> CustomAttributes { get; }

        /// <summary>
        /// Filters the custom attributes of this member.
        /// </summary>
        IEnumerable<T> GetCustomAttributes<T>();

        /// <summary>
        /// Tries to compute the nullability info of this member.
        /// This checks that the <see cref="NullabilityInfo.ReadState"/> is the same as
        /// the <see cref="NullabilityInfo.WriteState"/>: no [AllowNull], [DisallowNull] or
        /// other nullability attributes must exist: an error log is emitted in such case.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>Null if Read/Write nullabilities differ.</returns>
        IExtNullabilityInfo? GetHomogeneousNullabilityInfo( IActivityMonitor monitor );

        /// <summary>
        /// Gets the homogeneous nullability information only if there is no difference between read and
        /// write status.
        /// <para>Use <see cref="GetHomogeneousNullabilityInfo(IActivityMonitor)"/> to log an error if nullabilities differ.</para>
        /// </summary>
        IExtNullabilityInfo? HomogeneousNullabilityInfo { get; }

        /// <summary>
        /// Gets the read nullability information.
        /// </summary>
        IExtNullabilityInfo ReadNullabilityInfo { get; }

        /// <summary>
        /// Gets the write nullability information.
        /// </summary>
        IExtNullabilityInfo WriteNullabilityInfo { get; }
    }
}
