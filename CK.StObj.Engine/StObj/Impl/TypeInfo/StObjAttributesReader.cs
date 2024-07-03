#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Engine\StObj\Impl\TypeInfo\StObjAttributesReader.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CK.Core;

namespace CK.Setup
{
    internal class StObjAttributesReader
    {
        /// <summary>
        /// Retrieves a <see cref="IRealObjectAttribute"/> from (potentially multiple) attributes on a type.
        /// If multiple attributes are defined, <see cref="IRealObjectAttribute.Requires"/>, <see cref="IRealObjectAttribute.Children"/>, and <see cref="IRealObjectAttribute.RequiredBy"/>
        /// are merged, but if their <see cref="IRealObjectAttribute.Container"/> are not null or if <see cref="IRealObjectAttribute.ItemKind"/> is not <see cref="DependentItemKind.Unknown"/> and differ, the 
        /// first one is kept and a log is emitted in the <paramref name="monitor"/>.
        /// </summary>
        /// <param name="objectType">The type for which the attribute must be found.</param>
        /// <param name="monitor">Logger that will receive the warning.</param>
        /// <param name="multipleContainerLogLevel"><see cref="CK.Core.LogLevel"/> when different containers are detected. By default a warning is emitted.</param>
        /// <returns>
        /// Null if no <see cref="IRealObjectAttribute"/> is set.
        /// </returns>
        static internal IRealObjectAttribute? GetStObjAttributeForExactType( Type objectType, IActivityMonitor monitor, LogLevel multipleContainerLogLevel = LogLevel.Warn )
        {
            if( objectType == null ) throw new ArgumentNullException( "objectType" );
            if( monitor == null ) throw new ArgumentNullException( "monitor" );

            var a = (IRealObjectAttribute[])objectType.GetCustomAttributes( typeof( IRealObjectAttribute ), false );
            if( a.Length == 0 ) return null;
            if( a.Length == 1 ) return a[0];

            IList<Type>? requires = null;
            IList<Type>? requiredBy = null;
            IList<Type>? children = null;
            IList<Type>? group = null;
            DependentItemKindSpec itemKind = DependentItemKindSpec.Unknown;
            Type? container = null;
            IRealObjectAttribute? containerDefiner = null;
            foreach( IRealObjectAttribute attr in a )
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
                        Debug.Assert( containerDefiner != null && containerDefiner.Container != null );
                        monitor.Log( multipleContainerLogLevel, $"Attribute {attr.GetType().Name} for type {objectType} specifies Container type '{attr.Container.Name}' but attribute {containerDefiner.GetType().Name} specifies Container type '{containerDefiner.Container.Name}'. Container remains '{containerDefiner.Container.Name}'." );
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
            var r = new RealObjectAttribute(){ Container = container, ItemKind = itemKind };
            if( requires != null ) r.Requires = requires.ToArray();
            if( requiredBy != null ) r.RequiredBy = requiredBy.ToArray();
            if( children != null ) r.Children = children.ToArray();
            if( group != null ) r.Groups = group.ToArray();
            return r;
        }

        static void CombineTypes( ref IList<Type>? list, Type[]? types )
        {
            if( types != null && types.Length > 0 )
            {
                if( list == null ) list = new List<Type>();
                list.AddRangeArray( types );
            }
        }

    }
}
