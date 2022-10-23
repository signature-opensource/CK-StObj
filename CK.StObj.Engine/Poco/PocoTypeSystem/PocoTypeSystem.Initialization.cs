using CK.Core;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;

namespace CK.Setup
{

    public sealed partial class PocoTypeSystem
    {
        /// <summary>
        /// Initializes this type system with the IPoco discovery result.
        /// All IPoco interfaces are registered, including the "abstract" ones that are considered union types
        /// of all the Poco families interfaces that support them.
        /// The IPoco interface itself and the IClosedPoco are registered as union types.
        /// <para>
        /// After this, any IPoco interface that is not already registered here is an excluded type: any type
        /// that uses it is considered excluded.
        /// </para>
        /// </summary>
        /// <param name="monitor"></param>
        /// <returns></returns>
        public bool Initialize( IPocoDirectory poco, IActivityMonitor monitor )
        {
            var allPrimaries = new PocoType.PrimaryPocoType[poco.Families.Count];
            var closedPrimaries = new PocoType.PrimaryPocoType[poco.Families.Count( f => f.IsClosedPoco )];
            // First, registers the primary and all concrete interfaces for a family.
            int iPrimary = 0;
            int iClosedPrimary = 0;
            foreach( var family in poco.Families )
            {
                // Primary index for the PocoType that will hold the
                // fields is the PrimaryInterface. Other interface
                // will be indexed with this PocoType as their first
                // SubTypes.
                var primary = PocoType.CreatePrimaryPoco( this, family );
                _cache.Add( primary.Type, primary );
                allPrimaries[iPrimary++] = primary;
                if( family.IsClosedPoco ) closedPrimaries[iClosedPrimary++] = primary;
                Debug.Assert( family.Interfaces[0].PocoInterface == primary.Type );
                var fTypes = new IConcretePocoType[family.Interfaces.Count];
                fTypes[0] = primary;
                for( int i = 1; i < fTypes.Length; ++i )
                {
                    var t = family.Interfaces[i].PocoInterface;
                    var c = PocoType.CreatePoco( this, primary, t );
                    _cache.Add( t, c );
                    fTypes[i - 1] = c;
                }
                primary.AllowedTypes = fTypes;
            }
            // Second, registers the "abstract" interfaces as union types.
            // An abstract interface can extend other extract interfaces, we
            // must track this.
            // This is a DAG, we don't need a 2 step process and can use directly
            // the cache.
            var abstractTypes = poco.OtherInterfaces.Keys;
            var allAbstracts = new List<IAbstractPocoType>();
            var closedAbstracts = new List<IAbstractPocoType>();
            foreach( var tInterface in abstractTypes )
            {
                EnsureAbstract( poco, abstractTypes, tInterface, allAbstracts, closedAbstracts );
            }
            // Third, registers the IPoco and IClosedPoco full sets.
            var all = PocoType.CreateAbstractPoco( this, typeof( IPoco ), allAbstracts.ToArray(), allPrimaries );
            _cache.Add( typeof( IPoco ), all );
            var allClosed = PocoType.CreateAbstractPoco( this, typeof( IClosedPoco ), closedAbstracts.ToArray(), closedPrimaries );
            _cache.Add( typeof( IClosedPoco ), allClosed );
            // Now that all IPoco are known, we can set their fields.
            var builder = new PocoPropertyBuilder( this );
            bool success = true;
            foreach( var p in allPrimaries )
            {
                Debug.Assert( p.FamilyInfo != null );
                PrimaryPocoField[] fields = new PrimaryPocoField[p.FamilyInfo.Properties.Count];
                foreach( var prop in p.FamilyInfo.Properties.Values )
                {
                    var f = builder.Build( monitor, p, prop );
                    if( f == null ) return false;
                    fields[prop.Index] = f;
                }
                success &= p.SetFields( monitor, _sharedWriter, fields );
            }
            // If no error occurred, we can now detect any instantiation cycle error.
            // We handle only IPoco since collection items are not instantiated
            // and records are struct: a cycle in struct is not possible.
            // If we support mutable classes as "class records", then this will have
            // to be revisited.
            if( success )
            {
                var detector = new PocoType.InstantiationCycleVisitor();
                foreach( var p in allPrimaries )
                {
                    detector.VisitRoot( monitor, p );
                    if( !detector.CheckValid( monitor ) )
                    {
                        success = false;
                        break;
                    }
                }
            }
            return success;
        }

        PocoType EnsureAbstract( IPocoDirectory poco,
                                 IEnumerable<Type> abstractTypes,
                                 Type tAbstract,
                                 List<IAbstractPocoType> allAbstracts,
                                 List<IAbstractPocoType> closedAbstracts )
        {
            if( !_cache.TryGetValue( tAbstract, out var result ) )
            {
                var families = poco.OtherInterfaces[tAbstract];
                var abstractSubTypes = abstractTypes.Where( a => a != tAbstract && tAbstract.IsAssignableFrom( a ) ).ToList();
                var subTypes = new IPocoType[abstractSubTypes.Count + families.Count];
                int i = 0;
                foreach( var other in abstractSubTypes )
                {
                    subTypes[i++] = EnsureAbstract( poco, abstractTypes, other, allAbstracts, closedAbstracts );
                }
                foreach( var f in families )
                {
                    subTypes[i++] = _cache[f.PrimaryInterface.PocoInterface];
                }
                var a = PocoType.CreateAbstractPoco( this, tAbstract, abstractSubTypes.Count, subTypes );
                _cache.Add( tAbstract, result = a );
                allAbstracts.Add( a );
                if( typeof( IClosedPoco ).IsAssignableFrom( tAbstract ) ) closedAbstracts.Add( a );
            }
            return result;
        }


    }

}
