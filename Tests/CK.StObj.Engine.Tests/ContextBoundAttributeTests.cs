using CK.Core;
using CK.Setup;
using CK.Testing;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using static CK.Testing.StObjEngineTestHelper;

#pragma warning disable CA1822 // Mark members as static
#pragma warning disable CA1018 // Mark attributes with AttributeUsageAttribute
#pragma warning disable IDE0051 // Remove unused private members
#pragma warning disable CA2211 // Non-constant fields should not be visible

namespace CK.StObj.Engine.Tests
{

    [TestFixture]
    public class ContextBoundAttributeTests
    {
        #region S1 

        public class AnAttributeWithInitializer : Attribute, IAttributeContextBoundInitializer
        {
            public static bool Initialized;

            public void Initialize( IActivityMonitor monitor, ITypeAttributesCache owner, MemberInfo m, Action<Type> alsoRegister )
            {
                Initialized = true;
                owner.Type.Should().Be( typeof( S1 ) );
                m.Name.Should().Be( "M" );
            }
        }

        public class S1 : IRealObject
        {

            [AnAttributeWithInitializer]
            void M() { }
        }

        [Test]
        public void direct_attribute_IAttributeContextBoundInitializer_Initialize_is_called()
        {
            AnAttributeWithInitializer.Initialized = false;
            var c = TestHelper.CreateStObjCollector( typeof( S1 ) );
            var r = TestHelper.GetSuccessfulResult( c );
            Debug.Assert( r.EngineMap != null );
            r.EngineMap.AllTypesAttributesCache.Values.Should().Contain( a => a.Type == typeof( S1 ) );
            AnAttributeWithInitializer.Initialized.Should().BeTrue();
        }

        #endregion

        #region S2
        /// <summary>
        /// This class is directly targeted by a ContextBoundDelegation.
        /// Its constructor does not have the ContextBoundDelegation attribute.
        /// This is typically implemented in a Engine assembly.
        /// </summary>
        public class DirectAttributeImpl : IAttributeContextBoundInitializer
        {
            public static bool Initialized;

            void IAttributeContextBoundInitializer.Initialize( IActivityMonitor monitor, ITypeAttributesCache owner, MemberInfo m, Action<Type> alsoRegister )
            {
                Initialized = true;
                owner.Type.Should().Be( typeof( S2 ) );
                m.Name.Should().Be( "M" );
            }
        }

        public class S2 : IRealObject
        {
            [ContextBoundDelegation( "CK.StObj.Engine.Tests.ContextBoundAttributeTests+DirectAttributeImpl, CK.StObj.Engine.Tests" )]
            void M() { }
        }


        [Test]
        public void ContextBoundDelegation_can_be_used_directly()
        {
            DirectAttributeImpl.Initialized = false;
            var c = TestHelper.CreateStObjCollector( typeof( S2 ) );
            var r = TestHelper.GetSuccessfulResult( c );
            Debug.Assert( r.EngineMap != null );
            r.EngineMap.AllTypesAttributesCache.Values.Should().Contain( a => a.Type == typeof( S2 ) );
            DirectAttributeImpl.Initialized.Should().BeTrue();
        }
        #endregion

        #region S3
        /// <summary>
        /// Standard implementation: the OneAttribute specializes the ContextBoundDelegationAttribute
        /// so that the association to OneAttributeImpl (that is typically in a totally independent assembly: an "Engine"), is
        /// transparent.
        /// </summary>
        public class OneAttributeImpl : IAttributeContextBoundInitializer
        {
            readonly OneAttribute _attribute;

            /// <summary>
            /// The implementation MUST accept its "source" attribute in its constructor.
            /// </summary>
            /// <param name="a"></param>
            public OneAttributeImpl( OneAttribute a )
            {
                _attribute = a ?? throw new ArgumentNullException( nameof( a ) );
            }

            public static bool Initialized;

            void IAttributeContextBoundInitializer.Initialize( IActivityMonitor monitor, ITypeAttributesCache owner, MemberInfo m, Action<Type> alsoRegister )
            {
                Initialized = true;
                owner.Type.Should().Be( typeof( S3 ) );
                m.Name.Should().Be( "M" );
            }
        }

        public class OneAttribute : ContextBoundDelegationAttribute, IAttributeContextBoundInitializer
        {
            public OneAttribute()
                : base( "CK.StObj.Engine.Tests.ContextBoundAttributeTests+OneAttributeImpl, CK.StObj.Engine.Tests" )
            {

            }

            public void Initialize( IActivityMonitor monitor, ITypeAttributesCache owner, MemberInfo m, Action<Type> alsoRegister )
            {
                throw new System.NotImplementedException( "This is never called since the delegated attribute OneAttributeImpl replaces this one." );
            }
        }

        public class S3 : IRealObject
        {
            [One]
            void M() { }
        }

        [Test]
        public void delegated_attribute_IAttributeContextBoundInitializer_Initialize_is_called_but_not_the_one_of_the_primary_attribute()
        {
            OneAttributeImpl.Initialized = false;
            var c = TestHelper.CreateStObjCollector( typeof( S3 ) );
            var r = TestHelper.GetSuccessfulResult( c );
            Debug.Assert( r.EngineMap != null );
            r.EngineMap.AllTypesAttributesCache.Values.Should().Contain( a => a.Type == typeof( S3 ) );
            OneAttributeImpl.Initialized.Should().BeTrue();
        }
        #endregion

        public interface IAttributeTypeSample { }

        #region S4
        /// <summary>
        /// OneCtorAttributeImpl uses constructor injection.
        /// </summary>
        public class OneCtorAttributeImpl : IAttributeTypeSample
        {
            readonly OneCtorAttribute _attribute;

            public static bool Constructed;

            public OneCtorAttributeImpl( ContextBoundAttributeTests thisTest, OneCtorAttribute a, ITypeAttributesCache owner, Type type, MethodInfo m )
            {
                _attribute = a ?? throw new ArgumentNullException( nameof( a ) );
                Constructed = true;

                // We use this attribute in the S5 and S6 scenario.
                type.Should().Match( t => t == typeof( S4 ) || t == typeof( S5 ) || t == typeof( IServiceWithAttributeOnMember ) );
                owner.Type.Should().Match( t => t == typeof( S4 ) || t == typeof( S5 ) || t == typeof( IServiceWithAttributeOnMember ) );
                if( type == typeof( S4 ) || type == typeof( S5 ) )
                {
                    m.Name.Should().Be( "M" );
                }
                else
                {
                    m.Name.Should().Be( "OnAnInterface" );
                }
            }
        }

        public class OneCtorAttribute : ContextBoundDelegationAttribute
        {
            public OneCtorAttribute()
                : base( "CK.StObj.Engine.Tests.ContextBoundAttributeTests+OneCtorAttributeImpl, CK.StObj.Engine.Tests" )
            {
            }
        }

        public class S4 : IRealObject
        {
            [OneCtor]
            void M() { }
        }

        [Test]
        public void delegated_attribute_handles_constructor_injection_instead_of_initialization()
        {
            OneCtorAttributeImpl.Constructed = false;
            var aspectProvidedServices = new SimpleServiceContainer();
            // Registers this AttributeTests.
            aspectProvidedServices.Add( this );
            var c = new StObjCollector( TestHelper.Monitor, aspectProvidedServices );
            c.RegisterType( typeof( S4 ) );

            var r = TestHelper.GetSuccessfulResult( c );
            Debug.Assert( r.EngineMap != null );
            r.EngineMap.AllTypesAttributesCache.Values.Should().Contain( a => a.Type == typeof( S4 ) );
            OneCtorAttributeImpl.Constructed.Should().BeTrue();
        }
        #endregion


        #region S5
        /// <summary>
        /// OneCtorAttributeImpl uses constructor injection.
        /// </summary>
        public class OtherCtorAttributeImpl : IAttributeContextBoundInitializer, IAttributeTypeSample
        {
            readonly OtherCtorAttribute _attribute;

            public static bool Constructed;
            public static bool Initialized;

            public OtherCtorAttributeImpl( OtherCtorAttribute a, ITypeAttributesCache owner )
            {
                _attribute = a ?? throw new ArgumentNullException( nameof( a ) );
                Constructed = true;
                owner.Type.Should().Be( typeof( S5 ) );

                owner.GetAllCustomAttributes<IAttributeTypeSample>().Should().BeEmpty( "In the constructor, no attribute are available." );
            }

            void IAttributeContextBoundInitializer.Initialize( IActivityMonitor monitor, ITypeAttributesCache owner, MemberInfo m, Action<Type> alsoRegister )
            {
                Initialized = true;
                owner.GetAllCustomAttributes<IAttributeTypeSample>().Should().HaveCount( 2, "In the IAttributeContextBoundInitializer.Initialize, other attributes are available!" );
            }
        }

        public class OtherCtorAttribute : ContextBoundDelegationAttribute
        {
            public OtherCtorAttribute()
                : base( "CK.StObj.Engine.Tests.ContextBoundAttributeTests+OtherCtorAttributeImpl, CK.StObj.Engine.Tests" )
            {
            }
        }

        public class S5 : IRealObject
        {
            [OtherCtor]
            void MOther() { }

            [OneCtor]
            void M() { }
        }

        [Test]
        public void delegated_attribute_handles_constructor_injection_AND_IAttributeContextBoundInitializer()
        {
            OneCtorAttributeImpl.Constructed = false;
            OtherCtorAttributeImpl.Constructed = false;
            OtherCtorAttributeImpl.Initialized = false;

            var aspectProvidedServices = new SimpleServiceContainer();
            // Registers this AttributeTests.
            aspectProvidedServices.Add( this );
            var c = new StObjCollector( TestHelper.Monitor, aspectProvidedServices );
            c.RegisterType( typeof( S5 ) );
            c.RegisterType( typeof( S4 ) );

            var r = TestHelper.GetSuccessfulResult( c );
            Debug.Assert( r.EngineMap != null );
            r.EngineMap.AllTypesAttributesCache.Values.SelectMany( x => x.GetAllCustomAttributes<IAttributeTypeSample>() ).Should().HaveCount( 3 );

            OneCtorAttributeImpl.Constructed.Should().BeTrue();
            OtherCtorAttributeImpl.Constructed.Should().BeTrue();
            OtherCtorAttributeImpl.Initialized.Should().BeTrue();
        }

        #endregion

        #region S6

        public interface IServiceWithAttributeOnMember : IAutoService
        {
            [OneCtor]
            void OnAnInterface();
        }

        public class S6 : IServiceWithAttributeOnMember
        {
            void IServiceWithAttributeOnMember.OnAnInterface()
            {
            }
        }

        [Test]
        public void Attributes_can_be_on_AutoService_interface_members()
        {
            OneCtorAttributeImpl.Constructed = false;

            var aspectProvidedServices = new SimpleServiceContainer();
            // Registers this AttributeTests (required by the OneCtorAttributeImpl constructor).
            aspectProvidedServices.Add( this );
            var c = new StObjCollector( TestHelper.Monitor, aspectProvidedServices );
            c.RegisterType( typeof( S6 ) );

            var r = TestHelper.GetSuccessfulResult( c );
            Debug.Assert( r.EngineMap != null );

            r.EngineMap.AllTypesAttributesCache.Values
                          .Select( attrs => attrs.Type )
                          // These 3 objects are systematically registered.
                          // The base EndpointDefinition of the DefaultEndpointDefinition that is a [CKTypeDefiner]
                          // is also registered.
                          .Where( t => !typeof( PocoDirectory ).IsAssignableFrom( t )
                                       && !typeof( EndpointTypeManager ).IsAssignableFrom( t )
                                       // This filters both the EndpointDefinition of the DefaultEndpointDefinition types.
                                       && !typeof( EndpointDefinition ).IsAssignableFrom( t ) )
                          .Should().BeEquivalentTo( new[] { typeof( S6 ), typeof( IServiceWithAttributeOnMember ) } );

            r.EngineMap.AllTypesAttributesCache.Values
                          .SelectMany( attrs => attrs.GetAllCustomAttributes<IAttributeTypeSample>() )
                          .Should().HaveCount( 1 );

            OneCtorAttributeImpl.Constructed.Should().BeTrue();
        }


        #endregion

        #region S7

        public interface IRealObjectWithAttributeOnMember : IRealObject
        {
            [OneCtor]
            void OnAnInterface();
        }

        public class S7 : IRealObjectWithAttributeOnMember
        {
            void IRealObjectWithAttributeOnMember.OnAnInterface()
            {
            }
        }

        [Test]
        public void Attributes_can_NOT_YET_be_on_IRealObject_interface_members()
        {
            Assume.That( false, "This has to be implemented if needed, but this may not be really useful: a IRealObject is unambiguously mapped to its single implementation." );
            // => This could be done in CKTypeCollector.RegisterObjectClassInfo().

            OneCtorAttributeImpl.Constructed = false;

            var aspectProvidedServices = new SimpleServiceContainer();
            // Registers this AttributeTests (required by the OneCtorAttributeImpl constructor).
            aspectProvidedServices.Add( this );
            var c = new StObjCollector( TestHelper.Monitor, aspectProvidedServices );
            c.RegisterType( typeof( S7 ) );

            var r = TestHelper.GetSuccessfulResult( c );
            Debug.Assert( r.EngineMap != null );

            r.EngineMap.AllTypesAttributesCache.Values.Select( attrs => attrs.Type ).Should().BeEquivalentTo(
                new[] { typeof( S7 ), typeof( IRealObjectWithAttributeOnMember ) } );
            r.EngineMap.AllTypesAttributesCache.Values
                .SelectMany( attrs => attrs.GetAllCustomAttributes<IAttributeTypeSample>() ).Should().HaveCount( 1 );

            OneCtorAttributeImpl.Constructed.Should().BeTrue();
        }

        #endregion

    }
}
