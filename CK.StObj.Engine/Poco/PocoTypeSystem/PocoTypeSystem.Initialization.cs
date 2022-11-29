using CK.Core;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;
using CK.CodeGen;
using static OneOf.Types.TrueFalseOrNull;

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
                // PrimaryPoco hold the fields and are the only registered type
                // for a Poco family.
                // Secondary Poco interfaces are simply erased at this level.
                // We use the cache to handle once for all the mapping from the family
                // interface types to the PrimaryPoco.
                //
                // Note: before 2022-11-06, a IConcretePoco was modeling these secondary
                //       interfaces. Removing them is because we should consider the interfaces
                //       that define a Poco family as an "implementation details": the final IPoco
                //       is what matters. If, for any reason, these interfaces are needed, they are
                //       captured at the PocoDirectory level and may be used to restore a detailed
                //       "IPoco definition" knowledge.
                //
                // We cannot decide here for the ObliviousType: first, all the fields have
                // to be resolved to know if we need an "oblivious companion type" or if this
                // is its own oblivious type.
                var primary = PocoType.CreatePrimaryPoco( this, family );
                Debug.Assert( family.Interfaces[0].PocoInterface == primary.Type );
                foreach( var i in family.Interfaces )
                {
                    _obliviousCache.Add( i.PocoInterface, primary );
                }
                allPrimaries[iPrimary++] = primary;
                if( family.IsClosedPoco ) closedPrimaries[iClosedPrimary++] = primary;
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
                EnsureAbstract( monitor, poco, abstractTypes, tInterface, allAbstracts, closedAbstracts );
            }
            // Third, registers the IPoco and IClosedPoco full sets.
            var all = PocoType.CreateAbstractPoco( monitor, this, typeof( IPoco ), allAbstracts.ToArray(), allPrimaries );
            _obliviousCache.Add( typeof( IPoco ), all );
            var allClosed = PocoType.CreateAbstractPoco( monitor, this, typeof( IClosedPoco ), closedAbstracts.ToArray(), closedPrimaries );
            _obliviousCache.Add( typeof( IClosedPoco ), allClosed );
            // Fourth, initializes the PrimaryPocoType.AbstractTypes.
            foreach( var p in allPrimaries )
            {
                int nbAbstracts = p.FamilyInfo.OtherInterfaces.Count;
                if( nbAbstracts > 0 )
                {
                    var abstracts = new IAbstractPocoType[nbAbstracts];
                    int idx = 0;
                    foreach( var a in p.FamilyInfo.OtherInterfaces )
                    {
                        abstracts[idx++] = (IAbstractPocoType)_obliviousCache[a];
                    }
                    p.AbstractTypes = abstracts;
                }
                else
                {
                    p.AbstractTypes = Array.Empty<IAbstractPocoType>();
                }
            }
            // Now that all IPoco are known, we can set their fields.
            var builder = new PocoPropertyBuilder( this );
            bool success = true;
            foreach( var p in allPrimaries )
            {
                Debug.Assert( p.FamilyInfo != null );
                bool hasNonObliviousFieldType = false;
                PrimaryPocoField[] fields = new PrimaryPocoField[p.FamilyInfo.Properties.Count];
                foreach( var prop in p.FamilyInfo.Properties.Values )
                {
                    var f = builder.Build( monitor, p, prop );
                    if( f == null ) return false;
                    fields[prop.Index] = f;
                    if( !f.Type.IsOblivious ) hasNonObliviousFieldType = true;
                }
                success &= p.SetFields( monitor, StringBuilderPool, fields, createFakeObliviousType: hasNonObliviousFieldType );
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
                        // As soon as one cycle is detected, we stop: this avoids
                        // any dependency on the cycle to be (stupidly) detected.
                        success = false;
                        break;
                    }
                }
            }
            return success;
        }

        IPocoType EnsureAbstract( IActivityMonitor monitor,
                                  IPocoDirectory poco,
                                  IEnumerable<Type> abstractTypes,
                                  Type tAbstract,
                                  List<IAbstractPocoType> allAbstracts,
                                  List<IAbstractPocoType> closedAbstracts )
        {
            if( !_obliviousCache.TryGetValue( tAbstract, out var result ) )
            {
                var families = poco.OtherInterfaces[tAbstract];
                var abstractSubTypes = abstractTypes.Where( a => a != tAbstract && tAbstract.IsAssignableFrom( a ) ).ToList();
                var subTypes = new IPocoType[abstractSubTypes.Count + families.Count];
                int i = 0;
                foreach( var other in abstractSubTypes )
                {
                    subTypes[i++] = EnsureAbstract( monitor, poco, abstractTypes, other, allAbstracts, closedAbstracts );
                }
                foreach( var f in families )
                {
                    subTypes[i++] = _obliviousCache[f.PrimaryInterface.PocoInterface];
                }
                var a = PocoType.CreateAbstractPoco( monitor, this, tAbstract, abstractSubTypes.Count, subTypes );
                _obliviousCache.Add( tAbstract, result = a );
                allAbstracts.Add( a );
                if( typeof( IClosedPoco ).IsAssignableFrom( tAbstract ) ) closedAbstracts.Add( a );
            }
            return result;
        }


    }

}
