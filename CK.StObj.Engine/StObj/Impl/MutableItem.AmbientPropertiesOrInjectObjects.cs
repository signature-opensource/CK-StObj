using System;
using System.Collections.Generic;
using System.Linq;
using CK.Core;
using System.Diagnostics;

namespace CK.Setup
{
    partial class MutableItem
    {
        /// <summary>
        /// Used to expose only the first items of the ultimate leaf MutableAmbientProperty list.
        /// The number of MutableAmbientProperty exposed is the number of AmbientPropertyInfo in the AmbientProperties list of the StObjTypeInfo of
        /// the MutableItem.
        /// </summary>
        class ListAmbientProperty : IReadOnlyList<MutableAmbientProperty>
        {
            readonly MutableItem _item;
            readonly int _count;

            public ListAmbientProperty( MutableItem item )
            {
                _item = item;
                _count = _item.Type.AmbientProperties.Count;
            }

            public int IndexOf( object item )
            {
                int idx = -1;
                MutableAmbientProperty a = item as MutableAmbientProperty;
                if( a != null
                    && a.Owner == _item._leafData.LeafSpecialization
                    && a.AmbientPropertyInfo.Index < _count )
                {
                    idx = a.AmbientPropertyInfo.Index;
                }
                return idx;
            }

            public MutableAmbientProperty this[int index]
            {
                get
                {
                    if( index >= _count ) throw new IndexOutOfRangeException();
                    return _item._leafData.AllAmbientProperties[index];
                }
            }

            public bool Contains( object item )
            {
                return IndexOf( item ) >= 0;
            }

            public int Count
            {
                get { return _count; }
            }

            public IEnumerator<MutableAmbientProperty> GetEnumerator()
            {
                return _item._leafData.AllAmbientProperties.Take( _count ).GetEnumerator();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        /// <summary>
        /// This is the clone of ListAmbientProperty above.
        /// </summary>
        class ListInjectSingleton : IReadOnlyList<MutableInjectObject>
        {
            readonly MutableItem _item;
            readonly int _count;

            public ListInjectSingleton( MutableItem item )
            {
                _item = item;
                _count = _item.Type.InjectObjects.Count;
            }

            public int IndexOf( object item )
            {
                int idx = -1;
                MutableInjectObject c = item as MutableInjectObject;
                if( c != null
                    && c.Owner == _item._leafData.LeafSpecialization
                    && c.InjecttInfo.Index < _count )
                {
                    idx = c.InjecttInfo.Index;
                }
                return idx;
            }

            public MutableInjectObject this[int index]
            {
                get
                {
                    if( index >= _count ) throw new IndexOutOfRangeException();
                    return _item._leafData.AllInjectObjects[index];
                }
            }

            public bool Contains( object item )
            {
                return IndexOf( item ) >= 0;
            }

            public int Count => _count; 

            public IEnumerator<MutableInjectObject> GetEnumerator()
            {
                return _item._leafData.AllInjectObjects.Take( _count ).GetEnumerator();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }

        /// <summary>
        /// Works on leaf only. 
        /// Registers all the DirectProperties values by calling RootGeneralization.AddPreConstructProperty: they will be set right before the call to StObjConstruct of the root of the inheritance chain.
        /// Registers all the InjectObjects (resolves the MutableItem) by calling AddPostBuildProperty on the most specialized leaf: these properties will be set after the whole graph
        /// will be created.
        /// For AmbientProperties, it is slightly more complicated: depending of the property, we will be able to set it before StObjConstruct (like DirectProperties) or only after the whole graph
        /// is created.
        /// - When the AmbientProperty is a mere value (not a StObj), we can call RootGeneralization.AddPreConstructProperty.
        /// - When the AmbientProperty is a StObj, depending on the resolved StObj's TrackAmbientPropertyMode, we call RootGeneralization.AddPreConstructProperty only if 
        /// we can be sure that the target StObj (not necessarily its specialization) will be constructed before this object.
        /// This is where the TrackedAmbientPropertyInfo is added to the target and where covariance is handled. 
        /// (This is also where, each time I look at this code, I ask myself "wtf...???" :-).)
        /// </summary>
        internal void ResolvePreConstructAndPostBuildProperties(
            IActivityMonitor monitor,
            BuildValueCollector valueCollector,
            IStObjValueResolver valueResolver )
        {
            Debug.Assert( Specialization == null && _leafData.LeafSpecialization == this, "We are on the ultimate (leaf) Specialization." );
            // Here we AddPreConstructProperty the current direct properties: they will be set on the final object before 
            // the call to StObjConstruct.
            // We flush the dictionary and the next calls to SetDirectProperty will be AddPostBuildProperty.
            // This enforces the conherency between build time and runtime.
            if( _leafData.DirectPropertiesToSet != null )
            {
                foreach( var k in _leafData.DirectPropertiesToSet )
                {
                    if( k.Value != System.Type.Missing ) RootGeneralization.AddPreConstructProperty( k.Key, k.Value, valueCollector ); 
                }
                _leafData.DirectPropertiesToSet.Clear();
            }
            foreach( var c in _leafData.AllInjectObjects )
            {
                MutableItem m = c.ResolveToStObj( monitor, EngineMap );
                if( m != null )
                {
                    AddPostBuildProperty( c.InjecttInfo.SettablePropertyInfo, m, valueCollector );
                }
            }

            // Use _ambientPropertiesEx to work on a fixed set of MutableAmbientProperty that 
            // correspond to the ones of this object (without the cached ones that may appear at the end of the list).
            foreach( var a in _ambientPropertiesEx )
            {
                EnsureCachedAmbientProperty( monitor, a.Type, a.Name, a );
                if( a.Value == System.Type.Missing )
                {
                    if( valueResolver != null ) valueResolver.ResolveExternalPropertyValue( monitor, a );
                }
                object value = a.Value;
                if( value == System.Type.Missing )
                {
                    if( !a.IsOptional ) monitor.Error( $"{a.ToString()}: Unable to resolve non optional." );
                }
                else
                {
                    // Ambient property setting: when it is a StObj, it depends on the relationship between the items.
                    MutableItem resolved = value as MutableItem;
                    // If the property value is a StObj, extracts its actual value.
                    if( resolved != null )
                    {
                        #region AmbientProperty is a StObj.

                        MutableItem highestSetSource = null;
                        MutableItem highestSetResolved = null; 

                        MutableItem source = this;
                        AmbientPropertyInfo sourceProp = a.AmbientPropertyInfo;
                        Debug.Assert( sourceProp.Index < source.Type.AmbientProperties.Count, "This is the way to test if the property is defined at the source level or not." );

                        // Walks up the chain to locate the most abstract compatible slice.
                        {
                            MutableItem genResolved = resolved.Generalization;
                            while( genResolved != null && sourceProp.PropertyType.IsAssignableFrom( genResolved.ObjectType ) )
                            {
                                resolved = genResolved;
                                genResolved = genResolved.Generalization;
                            }
                        }
                        if( resolved._trackedAmbientProperties != null )
                        {
                            if( resolved._trackAmbientPropertiesMode == TrackAmbientPropertiesMode.AddPropertyHolderAsChildren 
                                || resolved._trackAmbientPropertiesMode == TrackAmbientPropertiesMode.PropertyHolderRequiresThis )
                            {
                                highestSetSource = source;
                                highestSetResolved = resolved;
                            }
                            resolved._trackedAmbientProperties.Add( new TrackedAmbientPropertyInfo( source, sourceProp ) );
                        }
                        // Walks up the source chain and adjusts the resolved target accordingly.
                        while( (source = source.Generalization) != null && resolved._needsTrackedAmbientProperties )
                        {
                            bool sourcePropChanged = false;
                            // If source does not define anymore sourceProp. Does it define the property with another type?
                            while( source != null && sourceProp.Index >= source.Type.AmbientProperties.Count )
                            {
                                sourcePropChanged = true;
                                if( (sourceProp = sourceProp.Generalization) == null )
                                {
                                    // No property defined anymore at this level: we do not have anything more to do.
                                    source = null;
                                }
                            }
                            if( source == null ) break;
                            Debug.Assert( sourceProp != null );
                            // Walks up the chain to locate the most abstract compatible slice.
                            if( sourcePropChanged )
                            {
                                MutableItem genResolved = resolved.Generalization;
                                while( genResolved != null && sourceProp.PropertyType.IsAssignableFrom( genResolved.ObjectType ) )
                                {
                                    resolved = genResolved;
                                    genResolved = genResolved.Generalization;
                                }
                            }
                            if( resolved._trackedAmbientProperties != null )
                            {
                                if( resolved._trackAmbientPropertiesMode == TrackAmbientPropertiesMode.AddPropertyHolderAsChildren 
                                    || resolved._trackAmbientPropertiesMode == TrackAmbientPropertiesMode.PropertyHolderRequiresThis )
                                {
                                    highestSetSource = source;
                                    highestSetResolved = resolved;
                                }
                                resolved._trackedAmbientProperties.Add( new TrackedAmbientPropertyInfo( source, sourceProp ) );
                            }
                        }
                        if( highestSetSource != null )
                        {
                            highestSetSource.AddPreConstructProperty( a.AmbientPropertyInfo.SettablePropertyInfo, highestSetResolved, valueCollector );
                        }
                        else
                        {
                            AddPostBuildProperty( a.AmbientPropertyInfo.SettablePropertyInfo, resolved, valueCollector );
                        }
                        #endregion 
                    }
                    else
                    {
                        RootGeneralization.AddPreConstructProperty( a.AmbientPropertyInfo.SettablePropertyInfo, value, valueCollector ); 
                    }
                }
            }
        }

        MutableAmbientProperty EnsureCachedAmbientProperty( IActivityMonitor monitor, Type propertyType, string name, MutableAmbientProperty alreadySolved = null )
        {
            Debug.Assert( Specialization == null );
            Debug.Assert( _prepareState == PrepareState.PreparedDone || _prepareState == PrepareState.CachingAmbientProperty );
            Debug.Assert( alreadySolved == null || (alreadySolved.Name == name && alreadySolved.Type == propertyType) );

            // Reentrancy is handled by returning null. 
            // The path that lead to such null result is simply ignored. 
            // Only the first entry point in the cycle will cache a new (invalid) MutableAmbientProperty( this, name ) in its cache. Any other call to the same cycle will lead to (and return) this (empty) marker.
            // The only other case where we return null is when the requested propertyType is not compatible with an existing cached property with the same name.
            if( _prepareState == PrepareState.CachingAmbientProperty ) return null;
            _prepareState = PrepareState.CachingAmbientProperty;
            try
            {
                MutableAmbientProperty a;
                if( alreadySolved != null )
                {
                    a = alreadySolved;
                }
                else
                {
                    a = _leafData.AllAmbientProperties.FirstOrDefault( p => p.Name == name );
                    if( a != null && !propertyType.IsAssignableFrom( a.Type ) )
                    {
                        monitor.Warn( $"Looking for property named '{name}' of type '{propertyType}': found a candidate on '{ToString()}' but type does not match (it is '{a.Type}'). It is ignored." );
                        return null;
                    }
                }
                // Never seen this property: we must find it in our containers (since it is not defined by any StObj in the specialization chain).
                if( a == null )
                {
                    MutableItem currentLevel = this;
                    do
                    {
                        if( currentLevel.IsOwnContainer ) a = currentLevel._dContainer._leafData.LeafSpecialization.EnsureCachedAmbientProperty( monitor, propertyType, name );
                        currentLevel = currentLevel.Generalization;
                    }
                    while( (a == null || a.Value == System.Type.Missing) && currentLevel != null );
                    if( a == null )
                    {
                        // Not found: registers the marker.
                        a = new MutableAmbientProperty( this, name );
                    }
                    _leafData.AllAmbientProperties.Add( a );
                    Debug.Assert( a.IsFinalValue );
                    // We necessarily leave here...
                }
                if( a.IsFinalValue ) return a;

                Debug.Assert( a.Value == System.Type.Missing || a.MaxSpecializationDepthSet > 0, "a Value exists => it has been set." );

                MutableAmbientProperty foundFromOther = null;
                // If the property has not been set to a value or not configured (a.MaxSpecializationDepthSet == 0), 
                // OR the value has been set to Missing to use resolution (this is a way to cancel any previous settings).
                if( a.MaxSpecializationDepthSet == 0 || (a.Value == System.Type.Missing && a.UseValue) )
                {
                    if( a.AmbientPropertyInfo.ResolutionSource == PropertyResolutionSource.FromGeneralizationAndThenContainer )
                    {
                        MutableItem currentLevel = _leafData.RootGeneralization;
                        do
                        {
                            if( currentLevel.IsOwnContainer ) foundFromOther = currentLevel._dContainer._leafData.LeafSpecialization.EnsureCachedAmbientProperty( monitor, propertyType, name );
                            currentLevel = currentLevel.Specialization;
                        }
                        while( (foundFromOther == null || foundFromOther.Value == System.Type.Missing) && currentLevel != null );
                    }
                    else if( a.AmbientPropertyInfo.ResolutionSource == PropertyResolutionSource.FromContainerAndThenGeneralization )
                    {
                        MutableItem currentLevel = this;
                        do
                        {
                            if( currentLevel.IsOwnContainer ) foundFromOther = currentLevel._dContainer._leafData.LeafSpecialization.EnsureCachedAmbientProperty( monitor, propertyType, name );
                            currentLevel = currentLevel.Generalization;
                        }
                        while( (foundFromOther == null || foundFromOther.Value == System.Type.Missing) && currentLevel != null );
                    }
                }
                // A Value exists: the property has been explicitly set or configured for resolution at a given level.
                // If we are in "FromContainerAndThenGeneralization" mode, before accepting the value or resolving it, we apply container's inheritance up to 
                // this level if it is not the most specialized one.
                // If not ("None" or "FromGeneralizationAndThenContainer") we have nothing to do.
                if( a.AmbientPropertyInfo.ResolutionSource == PropertyResolutionSource.FromContainerAndThenGeneralization && a.MaxSpecializationDepthSet < Type.SpecializationDepth )
                {
                    MutableItem currentLevel = this;
                    do
                    {
                        if( currentLevel.IsOwnContainer ) foundFromOther = currentLevel._dContainer._leafData.LeafSpecialization.EnsureCachedAmbientProperty( monitor, propertyType, name );
                        currentLevel = currentLevel.Generalization;
                    }
                    while( (foundFromOther == null || foundFromOther.Value == System.Type.Missing) && currentLevel != null && currentLevel.Type.SpecializationDepth > a.MaxSpecializationDepthSet );
                }
                if( foundFromOther != null && foundFromOther.Value != System.Type.Missing )
                {
                    a.SetValue( foundFromOther.Value );
                }
                else
                {
                    // No value found from containers or generalization: we may have to solve it.
                    a.SetValue( a.UseValue ? a.Value : a.ResolveToStObj( monitor, EngineMap ) );
                }
                return a;
            }
            finally
            {
                _prepareState = PrepareState.PreparedDone;
            }
        }

    }
}
