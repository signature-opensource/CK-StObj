

                // This is the very first step of the properties analysis.

                    // Basic checks that don't require all IPoco to be discovered (kind of fail fast).
                    //
                    // Before support of "AbstractReadOnlyProperties", we were rejecting here:
                    //  - Nullable property with error "Property type is nullable and readonly (it has no setter). This is forbidden since value will always be null".
                    //  - isBasicProperty: with error "Property type is a readonly basic type (it has no setter). This is forbidden since totally useless".
                    //  - isStandardCollection && !isReadonlyCompliantCollection (i.e an array) with error
                    //    "a readonly array (it has no setter). This is forbidden since we could only generate an empty array".
                    //
                    // AbstractReadOnlyProperties handles this as long as a writable property eventually with a compatible type exists on at least one interface
                    // of the IPoco.
                    //
                    //  - isStandardCollection && !isReadonlyCompliantCollection (i.e an array) with error
                    //    "a readonly array (it has no setter). This is forbidden since we could only generate an empty array".
                    //
                    // We change the message here to be "is a readonly array but a readonly array doesn't prevent its content to be mutated and is unsafe regarding variance. Use a IReadOnlyList instead to express immutability.".
                    //
                    //  - typeof( IPoco ).IsAssignableFrom( p.PropertyType ) && expanded.Contains( p.PropertyType ) with error:
                    //  "Poco Cyclic dependency error: readonly property references its own Poco type.".
                    //
                    // This last one is like the 2 first cases: as long as a writable property exists, this CAN work.

                //
                // A PocoPropertyImpl is the property defined on the interface whereas a PocoProperty is
                // the IPoco's property: a PocoProperty has one or more PocoPropertyImpl that define it.
                //
                // PocoPropertyImpl and PocoProperty:
                //   - have a "type":
                //      - a single NullableTypeTree (hence can be nullable or not)
                //      - or set of NullableTypeTree that defines an union type.
                //   - be Writable xor ReadOnly.
                //   - have a DefaultValue.
                //
                // PocoProperty can be "CtorSet" or not.
                //
                //
                // All writable PocoPropertyImpl of a PocoProperty must have the same type (invariance of the type), however
                // we cannot handle this here because of polymorphism: we need to consider IPoco families and have
                // to wait that all of them have been registered. And it is the same for the Poco-like: it's easier
                // to consider them later.
                //
                // ReadOnly PocoPropertyImpl are quite different beasts that can be supported in 2 ways:
                //  - By a writable PocoPropertyImpl (from another interface). In such case, their type
                //    must be compatible with the writable property's type.
                //  - By the generated constructor because their type is instantiable. This is the case of IPoco, Poco-Like,
                //    HashSet<>, List<> and Dictionary<,> of Poco compliant types.
                //  - By another ReadOnly PocoPropertyImpl that is "CtorInstantiable". Here also, their type
                //    must be compatible with the PocoProperty's type.   
                // 
                //  The "CtorInstantiable" aspect of a property solely depends on its type: IPoco, Poco-Like,
                //  HashSet<>, List<> and Dictionary<,> of Poco compliant types are "CtorInstantiable".
                //  However a "CtorInstantiable" property is not necessarily "CtorInstantiated": if the property is writable and
                //  nullable, we should not instantiate it (its default value is the null).
                //      CtorInstantiated <=> CtorInstantiable && (property is not writable || property is not nullable)
                //
                // The "CtorSet" aspect of a property is a generalization of this that consider the DefaultValue for basic types
                // (or type that has an associated converter with [DefaultValue( Type, string )] overload).
                //      CtorSet <=> CtorInstantiated || HasDefaultValue
                //
                // A non nullable property is necessarily "CtorSet".
                // 
