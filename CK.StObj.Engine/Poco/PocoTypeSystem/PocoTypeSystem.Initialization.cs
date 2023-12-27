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
                //  Note: The 2023-12-13, the "IConcretePoco" is back and named ISecondaryPocoType...
                //        Because we want ISuperUserInfo (that is a IUserInfo) to appear in the system type.
                //        Eradicating secondary interfaces worked fine, except that such "specialization" cannot
                //        appear in the exposed types. We eventually considered that this was not a good idea and
                //        had to handle this. The ISecondaryPocoType has its primary as its oblivious type, and
                //        relay IsSameType/IsReadableType/IsWritableType and DefaultValueInfo to its primary.
                //        
                // 
                // We also index the primary by its generated PocoClass type.
                //
                var primary = PocoType.CreatePrimaryPoco( this, family );
                Throw.DebugAssert( family.Interfaces[0].PocoInterface == primary.Type );
                _typeCache.Add( primary.Type, primary );
                _typeCache.Add( primary.CSharpName, primary );
                foreach( var i in family.Interfaces.Skip( 1 ) )
                {
                    var sec = PocoType.CreateSecondaryPocoType( this, i.PocoInterface, primary );
                    _typeCache.Add( i.PocoInterface, sec );
                    _typeCache.Add( sec.CSharpName, sec );
                }
                // Extra registration for the implementation class type.
                _typeCache.Add( family.PocoClass, primary );

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
            _typeCache.Add( typeof( IPoco ), all );
            var allClosed = PocoType.CreateAbstractPoco( monitor, this, typeof( IClosedPoco ), closedAbstracts.ToArray(), closedPrimaries );
            _typeCache.Add( typeof( IClosedPoco ), allClosed );
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
                        abstracts[idx++] = (IAbstractPocoType)_typeCache[a];
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
                PrimaryPocoField[] fields = new PrimaryPocoField[p.FamilyInfo.Properties.Count];
                foreach( var prop in p.FamilyInfo.Properties.Values )
                {
                    var f = builder.Build( monitor, p, prop );
                    // Continue to analyze the fields.
                    success &= f != null;
                    fields[prop.Index] = f!;
                }
                if( success )
                {
                    p.SetFields( monitor, fields );
                }
                else
                {
                    // We are on an error path. This Poco is invalid, the whole type system will be.
                    // We can be inefficient here!
                    fields = fields.Where( f => f != null ).ToArray();
                    p.SetFields( monitor, fields );
                }
            }
            // Oblivious check: all reference type registered by types are non nullable.
            // Only Value Types are oblivious and registered for nullable and non nullable.
            Throw.DebugAssert( _typeCache.Where( kv => kv.Key is Type )
                                         .Select( kv => (Type: (Type)kv.Key, PocoType: kv.Value) )
                                         .All( x => (x.PocoType.IsOblivious || x.PocoType.Kind == PocoTypeKind.SecondaryPoco )
                                                    &&
                                                    (
                                                        (x.Type.IsValueType && (x.PocoType.IsNullable == (Nullable.GetUnderlyingType(x.Type) != null)))
                                                        ||
                                                        (!x.Type.IsValueType && !x.PocoType.IsNullable)
                                                    )
                                             ) );
            // Even if an error occurred, we can detect any instantiation cycle error and missing defaults
            // (fields on errors have been filtered out).
            // We handle only cycle of IPoco since collection items are not instantiated
            // and records are struct: a cycle in struct is not possible.
            // If we support mutable classes as "class records", then this will have
            // to be revisited.
            // Since we visit the IPoco fields and its record types, we also handle missing default values
            // (any field that has a true DefaultValueInfo.IsDisallowed).
            bool cycleError = false;
            var detector = new PocoType.PocoCycleAndDefaultVisitor();
            foreach( var p in allPrimaries )
            {
                detector.VisitRoot( monitor, p );
                // As soon as one cycle is detected, we stop reporting it:
                // this avoids any dependency on the cycle to be (redundantly) detected
                // but we continue the process to detect any missing default value.
                success &= detector.CheckValid( monitor, ref cycleError );
                // If any error is detected, it is useless to compute any
                // constructor code.
                if( success ) p.ComputeCtorCode( StringBuilderPool );
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
            if( !_typeCache.TryGetValue( tAbstract, out var result ) )
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
                    subTypes[i++] = _typeCache[f.PrimaryInterface.PocoInterface];
                }
                var a = PocoType.CreateAbstractPoco( monitor, this, tAbstract, abstractSubTypes.Count, subTypes );
                result = a;
                _typeCache.Add( tAbstract, a );
                allAbstracts.Add( a );
                if( typeof( IClosedPoco ).IsAssignableFrom( tAbstract ) ) closedAbstracts.Add( a );
            }
            return result;
        }


    }

}
