#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Runtime\IStObjResult.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System.Collections.Generic;
using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// A StObj "slices" a Structured Object (that is an <see cref="IRealObject"/>) by types in its inheritance chain.
    /// The <see cref="InitialObject">Structured Object</see> itself is built based on already built dependencies from top 
    /// to bottom thanks to its "StObjConstruct" (<see cref="StObjContextRoot.ConstructMethodName"/>) methods. 
    /// This interface is available after the dependency graph ordering (this is the Owner exposed by <see cref="IStObjFinalParameter"/> for construct parameters for instance).
    /// It is the final interface that is exposed for each StObj at the end of the StObjCollector.GetResults work.
    /// </summary>
    public interface IStObjResult : IStObj
    {
        /// <summary>
        /// Gets the associated object instance (the final, most specialized, structured object).
        /// This instance is built at the beginning of the process and remains the same: it is not necessarily a "real" object since its auto-implemented methods
        /// are not generated (only stupid default stub implementation are created to be able to instanciate it).
        /// </summary>
        object InitialObject { get; }

        /// <summary>
        /// Gets the provider for attributes. Attributes that are marked with <see cref="IAttributeContextBound"/> are cached
        /// and can keep an internal state if needed.
        /// </summary>
        /// <remarks>
        /// All attributes related to this <see cref="IStObj.ObjectType"/> (either on the type itself or on any of its members)
        /// should be retrieved thanks to this method otherwise stateful attributes will not work correctly.
        /// </remarks>
        ICKCustomAttributeTypeMultiProvider Attributes { get; }

        /// <summary>
        /// Gets kind of structure object for this StObj. It can be a <see cref="DependentItemKindSpec.Item"/>, 
        /// a <see cref="DependentItemKindSpec.Group"/> or a <see cref="DependentItemKindSpec.Container"/>.
        /// </summary>
        DependentItemKindSpec ItemKind { get; }

        /// <summary>
        /// Gets the parent <see cref="IStObjResult"/> in the inheritance chain (the one associated to the base class of this <see cref="IStObj.ObjectType"/>).
        /// May be null.
        /// </summary>
        new IStObjResult Generalization { get; }

        /// <summary>
        /// Gets the child <see cref="IStObjResult"/> in the inheritance chain.
        /// May be null.
        /// </summary>
        new IStObjResult Specialization { get; }

        /// <summary>
        /// Gets the ultimate generalization <see cref="IStObjResult"/> in the inheritance chain. Never null (can be this object itself).
        /// </summary>
        IStObjResult RootGeneralization { get; }

        /// <summary>
        /// Gets the ultimate specialization <see cref="IStObjResult"/> in the inheritance chain. Never null (can be this object itself).
        /// </summary>
        IStObjResult LeafSpecialization { get; }

        /// <summary>
        /// Gets the configured container for this object. If this <see cref="Container"/> has been inherited 
        /// from its <see cref="Generalization"/>, this ConfiguredContainer is null.
        /// </summary>
        IStObjResult ConfiguredContainer { get; }

        /// <summary>
        /// Gets the container of this object. If no container has been explicitly associated for the object, this is the
        /// container of its <see cref="Generalization"/> (if it exists). May be null.
        /// </summary>
        IStObjResult Container { get; }

        /// <summary>
        /// Gets a list of required objects. This list combines the requirements of this items (explicitly required types, 
        /// construct parameters, etc.) and any RequiredBy from other objects.
        /// </summary>
        IReadOnlyList<IStObjResult> Requires { get; }

        /// <summary>
        /// Gets a list of Group objects to which this object belongs.
        /// </summary>
        IReadOnlyList<IStObjResult> Groups { get; }

        /// <summary>
        /// Gets a list of children objects when this <see cref="ItemKind"/> is either a <see cref="DependentItemKindSpec.Group"/> or a <see cref="DependentItemKindSpec.Container"/>.
        /// </summary>
        IReadOnlyList<IStObjResult> Children { get; }

        /// <summary>
        /// Gets the list of Ambient Properties that reference this object.
        /// </summary>
        IReadOnlyList<IStObjTrackedAmbientPropertyInfo> TrackedAmbientProperties { get; }

        /// <summary>
        /// Gets the value of the named property that may be associated to this StObj or to any StObj 
        /// in <see cref="Container"/> or <see cref="Generalization"/> 's chains (recursively).
        /// Null is a valid property value: <see cref="System.Type.Missing"/> is returned if the property is NOT defined.
        /// </summary>
        /// <param name="propertyName">Name of the property. Must not be null nor empty.</param>
        /// <returns>The <see cref="System.Type.Missing"/> marker if the property has not been defined.</returns>
        object GetStObjProperty( string propertyName );
        
    }
}
