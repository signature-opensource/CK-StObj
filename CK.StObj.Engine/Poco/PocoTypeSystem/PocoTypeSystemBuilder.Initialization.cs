using CK.Core;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;
using System.Reflection.Metadata;
using System.Collections.Immutable;

namespace CK.Setup
{

    public sealed partial class PocoTypeSystemBuilder
    {
        /// <summary>
        /// Initializes this type system with the IPoco discovery result.
        /// All discovered IPoco interfaces are registered.
        /// </summary>
        /// <param name="monitor">Required monitor.</param>
        /// <returns>True on success, false otherwise.</returns>
        public bool Initialize( IActivityMonitor monitor )
        {
            var allPrimaries = new PocoType.PrimaryPocoType[_pocoDirectory.Families.Count];
            // First, registers the primary and all concrete interfaces for a family.
            int iPrimary = 0;
            foreach( var family in _pocoDirectory.Families )
            {
                // PrimaryPoco hold the fields and are the only registered type
                // for a Poco family.
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
                HandleNonSerializedAndNotExchangeableAttributes( monitor, primary );
                Throw.DebugAssert( family.Interfaces[0].PocoInterface == primary.Type );
                _typeCache.Add( primary.Type, primary );
                _typeCache.Add( primary.CSharpName, primary );
                foreach( var i in family.Interfaces.Skip( 1 ) )
                {
                    var sec = PocoType.CreateSecondaryPocoType( this, i.PocoInterface, primary );
                    HandleNonSerializedAndNotExchangeableAttributes( monitor, sec );
                    _typeCache.Add( i.PocoInterface, sec );
                    _typeCache.Add( sec.CSharpName, sec );
                }
                // Extra registration for the implementation class type.
                _typeCache.Add( family.PocoClass, primary );

                allPrimaries[iPrimary++] = primary;
            }
            // Second, registers the "abstract" interfaces.
            // An abstract interface can extend other extract interfaces, we
            // must track this.
            // This is a DAG, we don't need a 2 step process and can use directly
            // the cache a simple recusive EnsureAbstract does the job.
            var abstractTypes = _pocoDirectory.OtherInterfaces.Keys;
            var allAbstracts = new List<IAbstractPocoType>();
            foreach( var tInterface in abstractTypes )
            {
                EnsureAbstract( monitor, abstractTypes, tInterface, allAbstracts );
            }
            // Third, registers the IPoco full sets.
            var all = PocoType.CreateAbstractPocoBase( monitor, this, allAbstracts, allPrimaries );
            _typeCache.Add( typeof( IPoco ), all );
            // Fourth, initializes the PrimaryPocoType.AbstractTypes.
            foreach( var p in allPrimaries )
            {
                int nbAbstracts = p.FamilyInfo.OtherInterfaces.Count;
                if( nbAbstracts > 0 )
                {
                    Throw.DebugAssert( p.FamilyInfo.OtherInterfaces.Distinct().Count() == p.FamilyInfo.OtherInterfaces.Count() );
                    var abstracts = new IAbstractPocoType[nbAbstracts];
                    IEnumerable<Type> otherInterfaces = p.FamilyInfo.OtherInterfaces;
                    int idx = 0;
                    foreach( var a in otherInterfaces )
                    {
                        abstracts[idx++] = (IAbstractPocoType)_typeCache[a];
                    }
                    p.SetAbstractTypes( abstracts );
                }
                else
                {
                    p.SetAbstractTypes( Array.Empty<IAbstractPocoType>() );
                }
            }
            // Now that all IPoco are known, we can resolve the generic type definition parameters:
            // these parameters are used for covariance detection, they must be resolved
            // before analyzing fields.
            bool success = true;
            foreach( var (t,d) in _typeDefinitions )
            {
                success &= d.InitializeGenericInstanceArguments( this, monitor );
            }
            // Now that all IPoco are known, we can initialize the Primary Poco fields.
            var builder = new PocoPropertyBuilder( this );
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
                    p.SetFields( fields );
                }
                else
                {
                    // We are on an error path. This Poco is invalid, the whole type system will also be invalid.
                    // We can be inefficient here!
                    fields = fields.Where( f => f != null ).ToArray();
                    p.SetFields( fields );
                }
            }
            // We are almost done. Now that the Primary Poco fields are resolved, we can resolve the
            // Abstract Poco fields: they are bound to their "main" type (resolved now as a PocoType)
            // and can obtain the set of implementation fields from the IPrimaryPoco.
            // Note: on error, we skip this step.
            if( success )
            {
                foreach( var any in allAbstracts )
                {
                    if( any is not PocoType.AbstractPocoType a ) continue;
                    success &= a.CreateFields( monitor, this );
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
            // (fields on error have been filtered out).
            // We handle only cycle of IPoco since collection items are not instantiated
            // and records are struct: a cycle in struct is not possible.
            // If we support mutable classes as "class records", then this will have
            // to be revisited.
            // Since we visit the IPoco fields and its record types, we also handle missing default values
            // (any field that has a true DefaultValueInfo.IsDisallowed).
            bool cycleError = false;
            var detector = new PocoCycleAndDefaultVisitor( _nonNullableTypes.Count );
            foreach( var p in allPrimaries )
            {
                detector.VisitRoot( p );
                // As soon as one cycle is detected, we stop reporting it:
                // this avoids any dependency on the cycle to be (redundantly) detected
                // but we continue the process to detect any missing default value.
                success &= detector.CheckValid( monitor, ref cycleError );
                // If any error is detected, it is useless to compute any
                // constructor code.
                if( success ) p.ComputeCtorCode( StringBuilderPool );
            }
            // If there's no error, bind the AbstractPoco fields.
            return success;
        }

        IPocoType EnsureAbstract( IActivityMonitor monitor,
                                  IEnumerable<Type> abstractTypes,
                                  Type tAbstract,
                                  List<IAbstractPocoType> allAbstracts )
        {
            if( !_typeCache.TryGetValue( tAbstract, out var result ) )
            {
                var families = _pocoDirectory.OtherInterfaces[tAbstract];
                var abstractSubTypes = abstractTypes.Where( a => a != tAbstract && tAbstract.IsAssignableFrom( a ) ).ToList();
                var subTypes = new IPocoType[abstractSubTypes.Count + families.Count];
                int i = 0;
                foreach( var other in abstractSubTypes )
                {
                    subTypes[i++] = EnsureAbstract( monitor, abstractTypes, other, allAbstracts );
                }
                foreach( var f in families )
                {
                    subTypes[i++] = _typeCache[f.PrimaryInterface.PocoInterface];
                }
                PocoType.PocoGenericTypeDefinition? typeDefinition = tAbstract.IsGenericType
                                                                        ? EnsureTypeDefinition( tAbstract.GetGenericTypeDefinition() )
                                                                        : null;
                var a = PocoType.CreateAbstractPoco( monitor, this, tAbstract, abstractSubTypes.Count, subTypes, typeDefinition );
                result = a;
                _typeCache.Add( tAbstract, a );
                allAbstracts.Add( a );
                HandleNonSerializedAndNotExchangeableAttributes( monitor, a );
            }
            return result;
        }

        PocoType.PocoGenericTypeDefinition EnsureTypeDefinition( Type tGen )
        {
            Throw.DebugAssert( tGen.IsInterface && tGen.IsGenericTypeDefinition );
            if( !_typeDefinitions.TryGetValue( (Type)tGen, out var typeDefinition ) )
            {
                Throw.DebugAssert( (bool)(tGen.ContainsGenericParameters && tGen.GetGenericArguments().Length > 0) );
                typeDefinition = PocoType.CreateGenericTypeDefinition( (Type)tGen );
                _typeDefinitions.Add( (Type)tGen, typeDefinition );
            }
            return typeDefinition;
        }
    }

}
