#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Engine\StObj\Impl\MutableAmbientProperty.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System;
using CK.Core;
using System.Diagnostics;

namespace CK.Setup
{
    /// <summary>
    /// Describes an Ambient property.
    /// </summary>
    internal class MutableAmbientProperty : MutableReferenceWithValue, IStObjAmbientProperty, IStObjFinalAmbientProperty
    {
        readonly AmbientPropertyInfo _info;
        int _maxSpecializationDepthSet;
        internal bool UseValue;

        internal MutableAmbientProperty( MutableItem owner, AmbientPropertyInfo info )
            : base( owner, StObjMutableReferenceKind.AmbientProperty )
        {
            _info = info;
            Type = _info.PropertyType;
            IsOptional = _info.IsOptional;
        }

        /// <summary>
        /// Initializes a new marker object: the ambient property has not been found. 
        /// </summary>
        internal MutableAmbientProperty( MutableItem owner, string unexistingPropertyName )
            : base( owner, StObjMutableReferenceKind.AmbientProperty )
        {
            _info = null;
            Type = typeof(object);
            IsOptional = false;
            _maxSpecializationDepthSet = Int32.MaxValue;
        }

        IStObjMutableItem IStObjAmbientProperty.Owner => Owner; 

        public override string Name => _info.Name;

        internal override string KindName => "AmbientProperty";

        internal override Type UnderlyingType => _info.PropertyType; 

        public override string ToString() => $"Ambient Property '{Name}' of '{Owner.ToString()}'";

        internal AmbientPropertyInfo AmbientPropertyInfo => _info;

        internal int MaxSpecializationDepthSet => _maxSpecializationDepthSet;

        /// <summary>
        /// Sets the final value. Public in order to implement IStObjFinalAmbientProperty.SetValue.
        /// </summary>
        public void SetValue( object value )
        {
            _maxSpecializationDepthSet = Int32.MaxValue;
            Value = value;
        }

        internal bool IsFinalValue
        {
            get { return _maxSpecializationDepthSet == Int32.MaxValue; }
        }

        internal bool SetValue( int setterSpecializationDepth, IActivityMonitor monitor, object value )
        {
            Debug.Assert( _maxSpecializationDepthSet != Int32.MaxValue );
            if( setterSpecializationDepth < _maxSpecializationDepthSet )
            {
                monitor.Error( $"'{ToString()}' has already been set or configured through a more specialized object." );
                return false;
            }
            _maxSpecializationDepthSet = setterSpecializationDepth;
            Value = value;
            UseValue = true;
            return true;
        }

        internal bool SetConfiguration( int setterSpecializationDepth, IActivityMonitor monitor, Type type, StObjRequirementBehavior behavior )
        {
            Debug.Assert( _maxSpecializationDepthSet != Int32.MaxValue );
            if( setterSpecializationDepth < _maxSpecializationDepthSet )
            {
                monitor.Error( $"'{this}' has already been set or configured through a more specialized object." );
                return false;
            }
            _maxSpecializationDepthSet = setterSpecializationDepth;
            Value = System.Type.Missing;
            Type = type;
            StObjRequirementBehavior = behavior;
            UseValue = false;
            return true;
        }

    }
}
