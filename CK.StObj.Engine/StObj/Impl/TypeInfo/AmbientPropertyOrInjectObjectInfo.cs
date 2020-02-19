using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using CK.Core;

namespace CK.Setup
{

    internal abstract class AmbientPropertyOrInjectObjectInfo : CovariantPropertyInfo
    {
        readonly bool _isOptionalDefined;
        bool _isOptional;

        internal AmbientPropertyOrInjectObjectInfo( PropertyInfo p, bool isOptionalDefined, bool isOptional, int definerSpecializationDepth, int index )
            : base( p, definerSpecializationDepth, index )
        {
            _isOptionalDefined = isOptionalDefined;
            _isOptional = isOptional;
        }

        public bool IsOptional => _isOptional; 

        protected override void SetGeneralizationInfo( IActivityMonitor monitor, CovariantPropertyInfo g )
        {
            base.SetGeneralizationInfo( monitor, g );
            AmbientPropertyOrInjectObjectInfo gen = (AmbientPropertyOrInjectObjectInfo)g;
            // A required property can not become optional.
            if( IsOptional && !gen.IsOptional )
            {
                if( _isOptionalDefined )
                {
                    monitor.Error( $"{Kind}: Property '{DeclaringType.FullName}.{Name}' states that it is optional but base property '{gen.DeclaringType.FullName}.{Name}' is required." );
                }
                _isOptional = false;
            }
        }

        /// <summary>
        /// An ambient property must be public or protected in order to be "specialized" either by overriding (for virtual ones)
        /// or by masking ('new' keyword in C#), typically to support covariance return type.
        /// The "Property Covariance" trick can be supported here because ambient properties are conceptually "read only" properties:
        /// they must be settable only to enable the framework (and no one else) to actually set their values.
        /// </summary>
        static public void CreateAmbientPropertyListForExactType( 
            IActivityMonitor monitor, 
            Type t, 
            int definerSpecializationDepth,
            CKTypeKindDetector ambientTypeKind,
            List<StObjPropertyInfo> stObjProperties, 
            out IList<AmbientPropertyInfo> apListResult,
            out IList<InjectObjectInfo> injectedListResult )
        {
            Debug.Assert( stObjProperties != null );
            
            var properties = t.GetProperties( BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly ).Where( p => !p.Name.Contains( '.' ) );
            apListResult = null;
            injectedListResult = null;
            foreach( var p in properties )
            {
                StObjPropertyAttribute stObjAttr = p.GetCustomAttribute<StObjPropertyAttribute>(false);
                if( stObjAttr != null )
                {
                    string nP = String.IsNullOrEmpty( stObjAttr.PropertyName ) ? p.Name : stObjAttr.PropertyName;
                    Type tP = stObjAttr.PropertyType == null ? p.PropertyType : stObjAttr.PropertyType;
                    if( stObjProperties.Find( sp => sp.Name == nP ) != null )
                    {
                        monitor.Error( $"StObj property named '{p.Name}' for '{p.DeclaringType}' is defined more than once. It should be declared only once." );
                        continue;
                    }
                    stObjProperties.Add( new StObjPropertyInfo( t, stObjAttr.ResolutionSource, nP, tP, p ) );
                    // Continue to detect Ambient properties. Properties that are both Ambient and StObj must be detected.
                }
                AmbientPropertyAttribute ap = p.GetCustomAttribute<AmbientPropertyAttribute>( false );
                IAmbientPropertyOrInjectObjectAttribute ac = p.GetCustomAttribute<InjectObjectAttribute>( false );
                if( ac != null || ap != null )
                {
                    if( stObjAttr != null || (ac != null && ap != null) )
                    {
                        monitor.Error( $"Property named '{p.Name}' for '{p.DeclaringType}' can not be both an Ambient Singleton, an Ambient Property or a StObj property." );
                        continue;
                    }
                    IAmbientPropertyOrInjectObjectAttribute attr = ac ?? ap;
                    string kindName = attr.IsAmbientProperty ? AmbientPropertyInfo.KindName : InjectObjectInfo.KindName;

                    var mGet = p.GetGetMethod( true );
                    if( mGet == null || mGet.IsPrivate )
                    {
                        monitor.Error( $"Property '{p.Name}' of '{p.DeclaringType}' can not be marked as {kindName}. Did you forget to make it protected or public?" );
                        continue;
                    }
                    if( attr.IsAmbientProperty )
                    {
                        if( apListResult == null ) apListResult = new List<AmbientPropertyInfo>();
                        var amb = new AmbientPropertyInfo( p, attr.IsOptionalDefined, attr.IsOptional, ap.IsResolutionSourceDefined, ap.ResolutionSource, definerSpecializationDepth, apListResult.Count );
                        apListResult.Add( amb );
                    }
                    else
                    {
                        if( injectedListResult == null ) injectedListResult = new List<InjectObjectInfo>();
                        var amb = new InjectObjectInfo( p, attr.IsOptionalDefined, attr.IsOptional, definerSpecializationDepth, injectedListResult.Count );
                        injectedListResult.Add( amb );
                    }
                }
            }
        }
    }

}
