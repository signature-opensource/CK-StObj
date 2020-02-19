using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Describes a <see cref="IStObjReference"/> that is an an ambient property. 
    /// There is no setter for the value: <see cref="IStObjMutableItem.SetAmbientPropertyValue"/> and <see cref="IStObjMutableItem.SetAmbientPropertyConfiguration"/> must 
    /// be used to update the configuration of an ambient property from <see cref="IStObjStructuralConfigurator.Configure"/>.
    /// </summary>
    public interface IStObjAmbientProperty : IStObjReference
    {
        /// <summary>
        /// Gets the item that owns this ambient property: the Owner is the ultimate (leaf) Specialization.
        /// </summary>
        /// <remarks>
        /// <para>
        /// For Ambient Properties the exposed Owner is the specialization, because a property has de facto more than one Owner when masking 
        /// is used: spotting one of them requires to choose among them - The more abstract one? The less abstract one? - and this would be 
        /// both ambiguate and quite useless since in practice, the "best Owner" must be based on the actual property type to 
        /// take Property Covariance into account.
        /// </para>
        /// </remarks>
        new IStObjMutableItem Owner { get; }

        /// <summary>
        /// Gets the name of the ambient property.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets whether the resolution of this property is optional.
        /// When it is true (see remarks) and the resolution fails, the property will not be set.
        /// </summary>
        /// <remarks>
        /// If this is true, it means that all property definition across the inheritance chain has [<see cref="AmbientPropertyAttribute">AmbientProperty</see>( <see cref="AmbientPropertyAttribute.IsOptional">IsOptional</see> = true ]
        /// attribute (from the most abstract property definition), because a required property can NOT become optional.
        /// (Note that the reverse is not true: an optional ambient property can perfectly be made required by Specializations.)
        /// </remarks>
        bool IsOptional { get; }
    }
}
