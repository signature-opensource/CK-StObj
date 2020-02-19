using System;

namespace CK.Core
{
    /// <summary>
    /// Defines that an real object must be injected: properties tagged with this attribute must 
    /// be <see cref="IRealObject"/> objects and are automatically injected.
    /// </summary>
    [AttributeUsage( AttributeTargets.Property, AllowMultiple = false, Inherited = true )]
    public class InjectObjectAttribute : Attribute, Setup.IAmbientPropertyOrInjectObjectAttribute
    {
        bool? _isOptional;

        /// <summary>
        /// Gets or sets whether finding the corresponding real object is required or not.
        /// Defaults to false (unless explicitly stated, the type must be resolved) but when 
        /// is not explicitly set to true or false on a specialized property its value is given by property 
        /// definition of the base class. 
        /// </summary>
        public bool IsOptional
        {
            get { return _isOptional.HasValue ? _isOptional.Value : false; }
            set { _isOptional = value; }
        }

        bool Setup.IAmbientPropertyOrInjectObjectAttribute.IsOptionalDefined => _isOptional.HasValue; 

        bool Setup.IAmbientPropertyOrInjectObjectAttribute.IsAmbientProperty => false; 
    }
}
