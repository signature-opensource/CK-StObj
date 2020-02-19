#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Engine\StObj\Impl\TypeInfo\AmbientPropertyInfo.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System.Reflection;
using CK.Core;

namespace CK.Setup
{
    internal class AmbientPropertyInfo : AmbientPropertyOrInjectObjectInfo
    {
        public new readonly static string KindName = "[AmbientProperty]";
        readonly bool _isSourceDefined;

        internal AmbientPropertyInfo( PropertyInfo p, bool isOptionalDefined, bool isOptional, bool isSourceDefined, PropertyResolutionSource source, int definerSpecializationDepth, int index )
            : base( p, isOptionalDefined, isOptional, definerSpecializationDepth, index )
        {
            _isSourceDefined = isSourceDefined;
            ResolutionSource = source;
        }

        /// <summary>
        /// Link to the ambient property above.
        /// </summary>
        public AmbientPropertyInfo Generalization { get; private set; }

        public PropertyResolutionSource ResolutionSource { get; private set; }

        protected override void SetGeneralizationInfo( IActivityMonitor monitor, CovariantPropertyInfo g )
        {
            base.SetGeneralizationInfo( monitor, g );

            AmbientPropertyInfo gen = (AmbientPropertyInfo)g;
            if( !_isSourceDefined ) ResolutionSource = gen.ResolutionSource;
            // Captures the Generalization.
            // We keep the fact that this property overrides one above (errors have been logged if conflict/incoherency occur).
            // We can keep the Generalization but not a reference to the specialization since we are 
            // not Contextualized here, but only on a pure Type level.
            Generalization = gen;
        }

        public override string Kind  => KindName; 
    }
}
