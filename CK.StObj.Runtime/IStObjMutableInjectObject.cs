using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Describes a <see cref="IStObjMutableReference"/> that is an injected real object: such references are defined by properties 
    /// marked with <see cref="InjectObjectAttribute"/>. The property type is necessarily a <see cref="IRealObject"/>
    /// (that typically use covariance between StObj layers) or a <see cref="ISingletonAutoService"/>. 
    /// </summary>
    public interface IStObjMutableInjectObject : IStObjMutableReference
    {
        /// <summary>
        /// Gets the name of the Ambient singleton property.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets whether the resolution of this property is optional.
        /// When it is true (see remarks) and the resolution fails, the property will not be set.
        /// </summary>
        /// <remarks>
        /// If this is true, it means that all property definition across the inheritance chain has [<see cref="InjectObjectAttribute">InjectAutoSingleton</see>( <see cref="IAmbientPropertyOrInjectObjectAttribute.IsOptional">IsOptional</see> = true ]
        /// attribute (from the most abstract property definition), because a required property can NOT become optional.
        /// (Note that the reverse is not true: an optional ambient property can perfectly be made required by Specializations.)
        /// </remarks>
        bool IsOptional { get; }

    }
}
