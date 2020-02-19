using System;
using System.Collections.Generic;
using System.Diagnostics;
using CK.Core;

namespace CK.Setup
{
    partial class MutableItem
    {
        class StObjProperty
        {
            static readonly object _unsetValue = typeof( StObjProperty );

            /// <summary>
            /// Null if this property results from a call to IStObjMutableItem.SetStObjPropertyValue 
            /// on the StObj of a Type that does not define the property (either by a property with a [StObjPropertyAttribute] 
            /// nor with a [StObjPropertyAttribute( PropertyName == ..., PropertyType = ...)] on the class itself.
            /// </summary>
            public readonly StObjPropertyInfo InfoOnType;
            public readonly string Name;
            public Type Type { get { return InfoOnType != null ? InfoOnType.Type : typeof( object ); } }
            object _value;

            public StObjProperty( string name, object value )
            {
                Name = name;
                _value = value;
                InfoOnType = null;
            }

            public StObjProperty( StObjPropertyInfo infoOnType )
            {
                Debug.Assert( infoOnType.Type != null );
                InfoOnType = infoOnType;
                Name = infoOnType.Name;
                _value = _unsetValue;
            }

            public bool HasStructuredObjectProperty
            {
                get { return InfoOnType != null && InfoOnType.PropertyInfo != null; }
            }

            public object Value
            {
                get { return _value == _unsetValue ? System.Type.Missing : _value; }
                set { _value = value; }
            }

            public bool ValueHasBeenSet
            {
                get { return _value != _unsetValue; }
            }
        }

        void SetStObjProperty( string propertyName, object value )
        {
            if( _stObjProperties == null )
            {
                _stObjProperties = new List<StObjProperty>();
                _stObjProperties.Add( new StObjProperty( propertyName, value ) );
            }
            else
            {
                int idx = _stObjProperties.FindIndex( o => o.Name == propertyName );
                if( idx >= 0 ) _stObjProperties[idx].Value = value;
                else _stObjProperties.Add( new StObjProperty( propertyName, value ) );
            }
        }

        void CheckStObjProperties( IActivityMonitor monitor, BuildValueCollector valueCollector )
        {
            if( _stObjProperties == null ) return;
            foreach( StObjProperty p in _stObjProperties )
            {
                if( p.InfoOnType == null || p.InfoOnType.ResolutionSource == PropertyResolutionSource.FromContainerAndThenGeneralization )
                {
                    // Check the Type constraint that could potentially hurt one day.
                    bool containerHasSetOrMerged = IsOwnContainer && HandleStObjPropertySource( monitor, p, _dContainer, "Container", true );
                    if( Generalization != null ) HandleStObjPropertySource( monitor, p, Generalization, "Generalization", !containerHasSetOrMerged );
                }
                else if( p.InfoOnType.ResolutionSource == PropertyResolutionSource.FromGeneralizationAndThenContainer )
                {
                    // Check the Type constraint that could potentially hurt one day.
                    bool generalizationHasSetOrMerged = Generalization != null && HandleStObjPropertySource( monitor, p, Generalization, "Generalization", true );
                    if( IsOwnContainer ) HandleStObjPropertySource( monitor, p, _dContainer, "Container", !generalizationHasSetOrMerged );
                }
                // If the value is missing (it has never been set or has been explicitly "removed"), we have nothing to do.
                // If the type is not constrained, we have nothing to do.
                object v = p.Value;
                if( v != System.Type.Missing )
                {
                    bool setIt = p.HasStructuredObjectProperty;
                    if( p.Type != typeof( object ) )
                    {
                        if( v == null )
                        {
                            if( p.Type.IsValueType && !(p.Type.IsGenericType && p.Type.GetGenericTypeDefinition() == typeof( Nullable<> )) )
                            {
                                monitor.Error( $"StObjProperty '{ToString()}.{p.Name}' has been set to null but its type '{p.Type.Name}' is not nullable." );
                                setIt = false;
                            }
                        }
                        else
                        {
                            if( !p.Type.IsAssignableFrom( v.GetType() ) )
                            {
                                monitor.Error( $"StObjProperty '{ToString()}.{p.Name}' is of type '{p.Type.Name}', but a value of type '{v.GetType()}' has been set." );
                                setIt = false;
                            }
                        }
                    }
                    if( setIt )
                    {
                        AddPreConstructProperty( p.InfoOnType.PropertyInfo, v, valueCollector );
                    }
                }
            }
        }

        private bool HandleStObjPropertySource( IActivityMonitor monitor, StObjProperty p, MutableItem source, string sourceName, bool doSetOrMerge )
        {
            StObjProperty c = source.GetStObjProperty( p.Name );
            // Source property is defined somewhere in the source.
            if( c != null )
            {
                // If the property is explicitly defined (Info != null) and its type is not 
                // compatible with our, there is a problem.
                if( c.InfoOnType != null && !p.Type.IsAssignableFrom( c.Type ) )
                {
                    // It is a warning because if actual values work, everything is okay... but one day, it should fail.
                    var msg = String.Format( "StObjProperty '{0}.{1}' of type '{2}' is not compatible with the one of its {6} ('{3}.{4}' of type '{5}'). Type should be compatible since {6}'s property value will be propagated if no explicit value is set for '{7}.{1}' or if '{3}.{4}' is set with an incompatible value.",
                        ToString(), p.Name, p.Type.Name,
                        _dContainer.Type.Type.Name, c.Name, c.Type.Name,
                        sourceName,
                        Type.Type.Name );
                    monitor.Warn( msg ); 
                }
                if( doSetOrMerge )
                {
                    // The source value must have been set and not explicitly "removed" with a System.Type.Missing value.
                    if( c.Value != System.Type.Missing )
                    {
                        // We "Set" the value from this source.
                        if( !p.ValueHasBeenSet ) p.Value = c.Value;
                        else if( p.Value is IMergeable )
                        {
                            using( var services = new SimpleServiceContainer() )
                            {
                                services.Add( monitor );
                                if( !((IMergeable)p.Value).Merge( c.Value, services ) )
                                {
                                    monitor.Error( $"Unable to merge StObjProperty '{ToString()}.{p.Value}' with value from {sourceName}." );
                                }
                            }
                        }
                        return true;
                    }
                }
            }
            return false;
        }

        StObjProperty GetStObjProperty( string propertyName, PropertyResolutionSource source = PropertyResolutionSource.FromContainerAndThenGeneralization )
        {
            if( _stObjProperties != null )
            {
                int idx = _stObjProperties.FindIndex( p => p.Name == propertyName );
                if( idx >= 0 ) return _stObjProperties[idx];
            }
            StObjProperty result = null;
            if( source == PropertyResolutionSource.FromContainerAndThenGeneralization )
            {
                result = IsOwnContainer ? _dContainer.GetStObjProperty( propertyName ) : null;
                if( result == null && Generalization != null ) result = Generalization.GetStObjProperty( propertyName );
            }
            else
            {
                result = Generalization != null ? Generalization.GetStObjProperty( propertyName, PropertyResolutionSource.FromGeneralizationAndThenContainer ) : null;
                if( result == null && IsOwnContainer ) result = _dContainer.GetStObjProperty( propertyName, PropertyResolutionSource.FromGeneralizationAndThenContainer );
            }
            return result;
        }

    }
}
