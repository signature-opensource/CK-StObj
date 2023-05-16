using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using CK.Setup;
using CK.Core;

#nullable enable annotations

namespace CK.Setup
{
    public partial class CKTypeCollector
    {
        readonly Dictionary<Type, MultipleImpl> _multipleMappings;

        /// <summary>
        /// To support IEnumerable&lt;T&gt; where T is [IsMultiple] with constraint propagations, we need to
        /// analyze the final implementations of the multiple interface.
        /// This is currently not exported, it only computes the AutoServiceKind of an IEnumerable of [IsMultiple] interfaces
        /// that are actually used by an <see cref="AutoServiceClassInfo"/> (not all of them are computed).
        /// </summary>
        internal class MultipleImpl : IMultipleInterfaceDescriptor
        {
            readonly List<CKTypeInfo> _rawImpls;
            readonly Type _enumType;
            readonly CKTypeKind _enumKind;
            readonly Type _itemType;
            readonly CKTypeKind _itemKind;
            AutoServiceKind _finalKind;
            bool _isComputed;
            bool _isComputing;

            internal MultipleImpl( Type tEnum, CKTypeKind enumKind, Type tItem, CKTypeKind itemKind, CKTypeInfo first )
            {
                _enumType = tEnum;
                _enumKind = enumKind;
                _itemType = tItem;
                _itemKind = itemKind;
                _rawImpls = new List<CKTypeInfo> { first };
                // These properties are null until ComputeFinalTypeKind is called.
                // (Offensive) I prefer assuming this nullity here rather than setting empty arrays.
                MarshallableTypes = null!;
            }

            public Type ItemType => _itemType;

            public Type EnumerableType => _enumType;

            public IReadOnlyCollection<Type> MarshallableTypes { get; private set; }

            /// <summary>
            /// Computes the final <see cref="AutoServiceKind"/>.
            /// This may fail (success is set to false) if the IEnumerable has been registered as a Singleton and one of the implementations is Scoped.
            /// Another issue (the worst case) is when a recursion is detected: then the "worst kind" for the IEnumerable
            /// is chosen that is a non marshallable IsProcessService|IsScoped.
            /// <para>
            /// The recursive "worst kind" is not a good choice. Handling this correctly is currently a complex unresolved issue.
            /// </para>
            /// </summary>
            /// <param name="m">The monitor to use.</param>
            /// <param name="kindComputeFacade">The CKTypeCollector facade.</param>
            /// <param name="success">The success flag that will be passed to implementation's <see cref="AutoServiceClassInfo.ComputeFinalTypeKind"/>.</param>
            /// <returns></returns>
            internal AutoServiceKind ComputeFinalTypeKind( IActivityMonitor m, IAutoServiceKindComputeFacade kindComputeFacade, ref bool success )
            {
                if( !_isComputed )
                {
                    if( _isComputing )
                    {
                        m.Warn( $"Automatic DI type of 'IEnumerable<{ItemType:C}> is not decidable: a dependency cycle has been found. It will be considered as the \"worst case\": a non marshallable process scoped service." );
                        _finalKind = AutoServiceKind.IsProcessService | AutoServiceKind.IsScoped;
                    }
                    else
                    {
                        // Check that the potential registered IEnumerable AutoServiceKind is compatible with the one of the enumerated interface.
                        var combined = (_itemKind | _enumKind) & ~CKTypeKind.IsMultipleService;
                        var conflict = combined.GetCombinationError( false );
                        if( conflict != null )
                        {
                            m.Error( $"Invalid configuration for 'IEnumerable<{ItemType:C}>' ({_enumKind}) that contradicts the {ItemType.Name} interface ({_itemKind}): {conflict}." );
                            success = false;
                        }
                        else
                        {
                            _isComputing = true;
                            DoComputeFinalTypeKind( m, kindComputeFacade, combined.ToAutoServiceKind(), ref success );
                            _isComputing = false;
                        }
                    }
                    _isComputed = true;
                }
                return _finalKind;
            }

            bool DoComputeFinalTypeKind( IActivityMonitor m, IAutoServiceKindComputeFacade ctx, AutoServiceKind initial, ref bool success )
            {
                Debug.Assert( _rawImpls != null );

                bool isScoped = (initial & AutoServiceKind.IsScoped) != 0;
                HashSet<Type>? allMarshallableTypes = null;
                // If it is [IsMarshallable], the marshaller must handle the marshalling of any implementations
                // (this is strange... but who knows?).
                bool isInterfaceMarshallable = (initial & AutoServiceKind.IsMarshallable) != 0;

                // If isInterfaceMarshallable is false (regular case), then for this IEnumerable to be marshallable, all its
                // implementations that are Process services must be marshallable so that it can be resolved as long as its
                // implementations have been marshalled.
                // Lets's be optimistic: all implementations that are Process services (if any) will be marshallable.
                bool isAutomaticallyMarshallable = true;

                using( m.OpenTrace( $"Computing 'IEnumerable<{ItemType.FullName}>'s final type from {_rawImpls.Count} implementations. Initial: '{initial}'." ) )
                {
                    foreach( var info in _rawImpls )
                    {
                        // RealObject are singleton, are not marshallable and not process service.
                        if( info is RealObjectClassInfo ) continue;

                        Debug.Assert( info.ServiceClass != null );
                        var impl = info.ServiceClass.MostSpecialized;
                        Debug.Assert( impl != null );
                        // We provide a new empty "cycle detection context" to the class constructors: IEnumerable
                        // of interfaces break potential cycles since they handle their own cycle by resolving to
                        // the "worst" non marshallable ProcessService|IsScoped.
                        // We consider that if the IEnumerable (or one of its class) cannot be resolved by the DI container,
                        // it's not our problem here.
                        var k = impl.ComputeFinalTypeKind( m, ctx, new Stack<AutoServiceClassInfo>(), ref success );
                        // Check for scope lifetime.
                        if( !isScoped )
                        {
                            if( (k & AutoServiceKind.IsScoped) != 0 )
                            {
                                if( (initial & AutoServiceKind.IsSingleton) != 0 )
                                {
                                    m.Error( $"Lifetime error: Type 'IEnumerable<{ItemType:C}>' has been registered as a Singleton but implementation '{impl.ClassType}' is Scoped." );
                                    success = false;
                                }
                                else
                                {
                                    isScoped = true;
                                    m.Info( $"Type 'IEnumerable<{ItemType:C}>' must be Scoped since the implementation '{impl.ClassType}' is Scoped." );
                                }
                            }
                        }
                        // If the implementation is not a front service, we skip it (we don't care of a IsMarshallable only type). 
                        if( (k & AutoServiceKind.IsProcessService) == 0 ) continue;

                        var newFinal = _finalKind | (k & AutoServiceKind.IsProcessService);
                        if( newFinal != _finalKind )
                        {
                            // Upgrades from None, Process to Front...
                            m.Trace( $"Type 'IEnumerable<{ItemType:C}>' must be IsProcessService, because of (at least) '{impl.ClassType}' implementation." );
                            _finalKind = newFinal;
                        }
                        // If the enumerated Service is marshallable at its level OR it is already known to be NOT automatically marshallable,
                        // we don't have to worry anymore about the subsequent implementations marshalling.
                        if( isInterfaceMarshallable || !isAutomaticallyMarshallable ) continue;

                        if( (k & AutoServiceKind.IsMarshallable) == 0 )
                        {
                            if( success ) m.Warn( $"Type 'IEnumerable<{ItemType:C}>' is not marshallable and the implementation '{impl.ClassType}' that is a Process service is not marshallable: 'IEnumerable<{ItemType.Name}>' cannot be considered as marshallable." );
                            isAutomaticallyMarshallable = false;
                        }
                        else
                        {
                            allMarshallableTypes ??= new HashSet<Type>();
                            Debug.Assert( impl.MarshallableTypes != null, "EnsureCtorBinding has been called." );
                            allMarshallableTypes.AddRange( impl.MarshallableTypes );
                        }
                    }
                    // Conclude about lifetime.
                    if( !isScoped )
                    {
                        if( success && (initial & AutoServiceKind.IsSingleton) == 0 )
                        {
                            m.Info( $"Nothing prevents 'IEnumerable<{ItemType:C}>' to be considered as a Singleton: this is the most efficient choice." );
                        }
                        _finalKind |= AutoServiceKind.IsSingleton;
                    }
                    else
                    {
                        _finalKind |= AutoServiceKind.IsScoped;
                    }
                    // Conclude about Front aspect.
                    if( isInterfaceMarshallable )
                    {
                        MarshallableTypes = new[] { ItemType };
                        Debug.Assert( (_finalKind & AutoServiceKind.IsMarshallable) != 0 );
                    }
                    else
                    {
                        if( isAutomaticallyMarshallable && allMarshallableTypes != null )
                        {
                            Debug.Assert( allMarshallableTypes.Count > 0 );
                            MarshallableTypes = allMarshallableTypes;
                            _finalKind |= AutoServiceKind.IsMarshallable;
                        }
                        else
                        {
                            // This service is not a Process service OR it is not automatically marshallable.
                            // We have nothing special to do: the set of Marshallable types is empty (this is not an error).
                            MarshallableTypes = Type.EmptyTypes;
                            Debug.Assert( (_finalKind & AutoServiceKind.IsMarshallable) == 0 );
                        }
                    }
                    if( _finalKind != initial ) m.CloseGroup( $"Final: {_finalKind}" );
                }

                return success;
            }

            internal void AddImpl( CKTypeInfo final ) => _rawImpls.Add( final );
        }

        MultipleImpl? IAutoServiceKindComputeFacade.GetMultipleInterfaceDescriptor( Type enumeratedType ) => _multipleMappings.GetValueOrDefault( enumeratedType );

        internal void RegisterMultipleInterfaces( Type tAbstraction, CKTypeKind enumeratedKind, CKTypeInfo final )
        {
            if( !_multipleMappings.TryGetValue( tAbstraction, out var multiple ) )
            {
                Debug.Assert( enumeratedKind.GetCombinationError( false ) == null );
                // A IEnumerable of IScoped is scoped, a IEnumerable of ISingleton is singleton, a IEnumerable of IProcessService is
                // itself a process service. Even a IEnumerable of an abstraction that has been marked [IsMarshallable] is de facto marshallable.
                // We rely on this here.
                Type tEnumerable = typeof( IEnumerable<> ).MakeGenericType( tAbstraction );

                // The IEnumerable itself may have been explicitly registered via SetAutoServiceKind.
                // We will check its compatibility with the enumerated item kind (there may be incoherences) later in the DoComputeFinalTypeKind.
                CKTypeKind enumKind = KindDetector.GetValidKind( monitor, tEnumerable );
                Debug.Assert( enumKind.GetCombinationError( false ) == null );
                _multipleMappings.Add( tAbstraction, new MultipleImpl( tEnumerable, enumKind, tAbstraction, enumeratedKind, final ) );
            }
            else
            {
                multiple.AddImpl( final );
            }
        }


    }


}
