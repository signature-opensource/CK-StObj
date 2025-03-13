using CK.Core;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CK.Setup;

/// <summary>
/// Unifies parameters, properties, fields, events, types (but mot methods)
/// and caches the custom attributes.
/// </summary>
public interface IExtMemberInfo
{
    /// <summary>
    /// Gets the underlying <see cref="PropertyInfo"/>, <see cref="FieldInfo"/>, <see cref="EventInfo"/>, <see cref="System.Type"/>
    /// or <see cref="ParameterInfo"/>.
    /// </summary>
    object UnderlyingObject { get; }

    /// <summary>
    /// Gets the Type that declares this member.
    /// <para>
    /// For <see cref="IExtParameterInfo"/> this is the type that declares the method.
    /// </para>
    /// <para>
    /// Caution: for <see cref="IExtTypeInfo"/> this is the type itself since we don't really care 
    /// whether the type is nested or not.
    /// </para>
    /// </summary>
    Type DeclaringType { get; }

    /// <summary>
    /// Gets the Type of the member.
    /// Note that <see cref="Type.IsByRef"/> may be true.
    /// <para>
    /// For <see cref="IExtTypeInfo"/> this is the type itself.
    /// </para>
    /// </summary>
    Type Type { get; }

    /// <summary>
    /// Gets the member name.
    /// <para>
    /// This is the empty string for a returned <see cref="ParameterInfo"/> by a method.
    /// </para>
    /// <para>
    /// This is the <see cref="MemberInfo.Name"/> for a <see cref="IExtTypeInfo"/>.
    /// </para>
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the member type C# name. This caches <see cref="CK.Core.TypeExtensions.ToCSharpName(Type?, bool, bool, bool)"/>,
    /// there is no handling of Nullable Reference Type here.
    /// <para>No ByRef marker (trailing &amp;) exists in this type name.</para>
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
    /// Gets the homogeneous nullability information only if there is no difference between read and
    /// write status.
    /// <para>Use <see cref="ExtMemberInfoExtensions.GetHomogeneousNullabilityInfo(IExtMemberInfo, IActivityMonitor)"/> to log an error if nullabilities differ.</para>
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
