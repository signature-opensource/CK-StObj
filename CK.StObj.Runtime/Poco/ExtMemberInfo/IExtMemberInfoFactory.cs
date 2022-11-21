using CK.Core;
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CK.Setup
{

    /// <summary>
    /// Factories for <see cref="IExtNullabilityInfo"/>.
    /// </summary>
    public interface IExtMemberInfoFactory
    {
        /// <summary>
        /// Creates the <see cref="IExtMemberInfo" /> for the given <see cref="ParameterInfo" />.
        /// </summary>
        /// <param name="parameterInfo">The parameter for which member info must be obtained.</param>
        /// <returns>The <see cref="IExtMemberInfo"/>.</returns>
        IExtParameterInfo Create( ParameterInfo parameterInfo );

        /// <summary>
        /// Creates the <see cref="IExtMemberInfo" /> for the given <see cref="PropertyInfo" />.
        /// </summary>
        /// <param name="propertyInfo">The property for which member info must be obtained.</param>
        /// <returns>The <see cref="IExtMemberInfo"/>.</returns>
        IExtPropertyInfo Create( PropertyInfo propertyInfo );

        /// <summary>
        /// Creates the <see cref="IExtMemberInfo" /> for the given <see cref="FieldInfo" />.
        /// </summary>
        /// <param name="fieldInfo">The field for which member info must be obtained.</param>
        /// <returns>The <see cref="IExtMemberInfo"/>.</returns>
        IExtFieldInfo Create( FieldInfo fieldInfo );

        /// <summary>
        /// Creates the <see cref="IExtMemberInfo" /> for the given <see cref="EventInfo" />.
        /// </summary>
        /// <param name="eventInfo">The event for which member info must be obtained.</param>
        /// <returns>The <see cref="IExtMemberInfo"/>.</returns>
        IExtEventInfo Create( EventInfo eventInfo );

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
    }
}
