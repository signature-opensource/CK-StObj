using CK.Core;
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CK.Setup;


/// <summary>
/// Factories for <see cref="IExtNullabilityInfo"/>.
/// </summary>
public interface IExtMemberInfoFactory
{
    /// <summary>
    /// Creates the <see cref="IExtMemberInfo" /> for the given <see cref="Type" />.
    /// The oblivious information is not nullable for all reference types.
    /// </summary>
    /// <param name="type">The type for which member info must be obtained.</param>
    /// <returns>The <see cref="IExtTypeInfo"/>.</returns>
    IExtTypeInfo CreateNullOblivious( Type type );

    /// <summary>
    /// Creates the <see cref="IExtMemberInfo" /> for a <see cref="MemberInfo" />.
    /// <para>
    /// Type, PropertyInfo, FieldInfo and EventInfo are MemberInfo but <see cref="ParameterInfo"/>
    /// is not. This will throw an <see cref="ArgumentException"/> for <see cref="MethodBase"/>.
    /// </para>
    /// </summary>
    /// <param name="memberInfo">The member for which info must be obtained.</param>
    /// <returns>The <see cref="IExtMemberInfo"/>.</returns>
    IExtMemberInfo Create( MemberInfo memberInfo );

    /// <summary>
    /// Creates the <see cref="IExtMemberInfo" /> for the given <see cref="ParameterInfo" />.
    /// </summary>
    /// <param name="parameterInfo">The parameter for which member info must be obtained.</param>
    /// <returns>The <see cref="IExtParameterInfo"/>.</returns>
    IExtParameterInfo Create( ParameterInfo parameterInfo );

    /// <summary>
    /// Creates the <see cref="IExtMemberInfo" /> for the given <see cref="PropertyInfo" />.
    /// </summary>
    /// <param name="propertyInfo">The property for which member info must be obtained.</param>
    /// <returns>The <see cref="IExtPropertyInfo"/>.</returns>
    IExtPropertyInfo Create( PropertyInfo propertyInfo );

    /// <summary>
    /// Creates the <see cref="IExtMemberInfo" /> for the given <see cref="FieldInfo" />.
    /// </summary>
    /// <param name="fieldInfo">The field for which member info must be obtained.</param>
    /// <returns>The <see cref="IExtFieldInfo"/>.</returns>
    IExtFieldInfo Create( FieldInfo fieldInfo );

    /// <summary>
    /// Creates the <see cref="IExtMemberInfo" /> for the given <see cref="EventInfo" />.
    /// </summary>
    /// <param name="eventInfo">The event for which member info must be obtained.</param>
    /// <returns>The <see cref="IExtEventInfo"/>.</returns>
    IExtEventInfo Create( EventInfo eventInfo );

    /// <summary>
    /// Creates a fake property info based on a real one.
    /// </summary>
    /// <param name="p">The property info.</param>
    /// <param name="homogeneousInfo">The nullability info. <see cref="IExtNullabilityInfo.IsHomogeneous"/> must be true.</param>
    /// <param name="customAttributes">Any custom attributes.</param>
    /// <param name="customAttributesData">Any custom attributes data.</param>
    /// <returns></returns>
    IExtPropertyInfo CreateFake( PropertyInfo p,
                                 IExtNullabilityInfo homogeneousInfo,
                                 object[]? customAttributes,
                                 CustomAttributeData[]? customAttributesData );

    /// <summary>
    /// Obtains the <see cref="IExtNullabilityInfo" /> for the given <see cref="ParameterInfo" />.
    /// See <see cref="NullabilityInfoContext.Create(ParameterInfo)"/>.
    /// </summary>
    /// <param name="parameterInfo">The parameter for which nullability info must be obtained.</param>
    /// <param name="useReadState">False to consider the <see cref="NullabilityInfo.WriteState"/> instead of the <see cref="NullabilityInfo.ReadState"/>.</param>
    /// <returns>The <see cref="IExtNullabilityInfo"/>.</returns>
    IExtNullabilityInfo CreateNullabilityInfo( ParameterInfo parameterInfo, bool useReadState = true );

    /// <summary>
    /// Obtains the <see cref="IExtNullabilityInfo" /> for the given <see cref="PropertyInfo" />.
    /// See <see cref="NullabilityInfoContext.Create(PropertyInfo)"/>.
    /// </summary>
    /// <param name="propertyInfo">The property for which nullability info must be obtained.</param>
    /// <param name="useReadState">False to consider the <see cref="NullabilityInfo.WriteState"/> instead of the <see cref="NullabilityInfo.ReadState"/>.</param>
    /// <returns>The <see cref="IExtNullabilityInfo"/>.</returns>
    IExtNullabilityInfo CreateNullabilityInfo( PropertyInfo propertyInfo, bool useReadState = true );

    /// <summary>
    /// Obtains the <see cref="IExtNullabilityInfo" /> for the given <see cref="FieldInfo" />.
    /// See <see cref="NullabilityInfoContext.Create(FieldInfo)"/>.
    /// </summary>
    /// <param name="fieldInfo">The field for which nullability info must be obtained.</param>
    /// <param name="useReadState">False to consider the <see cref="NullabilityInfo.WriteState"/> instead of the <see cref="NullabilityInfo.ReadState"/>.</param>
    /// <returns>The <see cref="IExtNullabilityInfo"/>.</returns>
    IExtNullabilityInfo CreateNullabilityInfo( FieldInfo fieldInfo, bool useReadState = true );

    /// <summary>
    /// Obtains the <see cref="IExtNullabilityInfo" /> for the given <see cref="EventInfo" />.
    /// See <see cref="NullabilityInfoContext.Create(EventInfo)"/>.
    /// </summary>
    /// <param name="eventInfo">The event for which nullability info must be obtained.</param>
    /// <returns>The <see cref="IExtNullabilityInfo"/>.</returns>
    IExtNullabilityInfo CreateNullabilityInfo( EventInfo eventInfo );

    /// <summary>
    /// Obtains an oblivious <see cref="IExtNullabilityInfo" /> for a type
    /// (all reference types are nullable).
    /// </summary>
    /// <param name="type">The type for which nullability info must be obtained.</param>
    /// <returns>The nullable <see cref="IExtNullabilityInfo"/>.</returns>
    IExtNullabilityInfo CreateNullabilityInfo( Type type );
}
