#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Engine\StObj\Impl\TypeInfo\StObjAttributesReader.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using CK.Core;

namespace CK.Setup
{
    internal class StObjAttributesReader
    {
        /// <summary>
        /// Retrieves a <see cref="IStObjAttribute"/> from (potentially multiple) attributes on a type.
        /// If multiple attributes are defined, <see cref="IStObjAttribute.Requires"/>, <see cref="IStObjAttribute.Children"/>, and <see cref="IStObjAttribute.RequiredBy"/>
        /// are merged, but if their <see cref="IStObjAttribute.Container"/> are not null or if <see cref="IStObjAttribute.ItemKind"/> is not <see cref="DependentItemKind.Unknown"/> and differ, the 
        /// first one is kept and a log is emitted in the <paramref name="monitor"/>.
        /// </summary>
        /// <param name="objectType">The type for which the attribute must be found.</param>
        /// <param name="monitor">Logger that will receive the warning.</param>
        /// <param name="multipleContainerLogLevel"><see cref="CK.Core.LogLevel"/> when different containers are detected. By default a warning is emitted.</param>
        /// <returns>
        /// Null if no <see cref="IStObjAttribute"/> is set.
        /// </returns>
        static internal IStObjAttribute GetStObjAttributeForExactType( Type objectType, IActivityMonitor monitor, LogLevel multipleContainerLogLevel = LogLevel.Warn )
        {
            if( objectType == null ) throw new ArgumentNullException( "objectType" );
            if( monitor == null ) throw new ArgumentNullException( "monitor" );

            var a = (IStObjAttribute[])objectType.GetCustomAttributes( typeof( IStObjAttribute ), false );
            if( a.Length == 0 ) return null;
            if( a.Length == 1 ) return a[0];

            IList<Type> requires = null;
            IList<Type> requiredBy = null;
            IList<Type> children = null;
            IList<Type> group = null;
            DependentItemKindSpec itemKind = DependentItemKindSpec.Unknown;
            Type container = null;
            IStObjAttribute containerDefiner = null;
            foreach( IStObjAttribute attr in a )
            {
                if( attr.Container != null )
                {
                    if( container == null )
                    {
                        containerDefiner = attr;
                        container = attr.Container;
                    }
                    else
                    {
                        if( monitor.ShouldLogLine( multipleContainerLogLevel ) )
                        {
                            string msg = $"Attribute {attr.GetType().Name} for type {objectType} specifies Container type '{attr.Container.Name}' but attribute {containerDefiner.GetType().Name} specifies Container type '{containerDefiner.Container.Name}'. Container remains '{containerDefiner.Container.Name}'.";
                            monitor.UnfilteredLog( ActivityMonitor.Tags.Empty, multipleContainerLogLevel, msg, monitor.NextLogTime(), null );
                        }
                    }
                }
                if( attr.ItemKind != DependentItemKindSpec.Unknown )
                {
                    if( itemKind != DependentItemKindSpec.Unknown ) monitor.Warn( $"ItemKind is already set to '{itemKind}'. Value '{attr.ItemKind}' set by {attr.GetType().Name} is ignored." );
                    else itemKind = attr.ItemKind;
                }
                CombineTypes( ref requires, attr.Requires );
                CombineTypes( ref requiredBy, attr.RequiredBy );
                CombineTypes( ref children, attr.Children );
                CombineTypes( ref group, attr.Groups );
            }
            var r = new StObjAttribute();
            r.Container = container;
            r.ItemKind = itemKind;
            if( requires != null ) r.Requires = requires.ToArray();
            if( requiredBy != null ) r.RequiredBy = requiredBy.ToArray();
            if( children != null ) r.Children = children.ToArray();
            if( group != null ) r.Groups = group.ToArray();
            return r;
        }

        static void CombineTypes( ref IList<Type> list, Type[] types )
        {
            if( types != null && types.Length > 0 )
            {
                if( list == null ) list = new List<Type>();
                list.AddRangeArray( types );
            }
        }

    }
}
