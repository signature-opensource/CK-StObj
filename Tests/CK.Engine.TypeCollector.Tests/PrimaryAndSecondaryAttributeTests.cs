using CK.Core;
using NUnit.Framework;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Engine.TypeCollector.Tests;


[TestFixture]
public class PrimaryAndSecondaryAttributeTests
{
    [AttributeMustSuffixTheNamePT]
    public class FailedP
    {
        [AttributeMustSuffixTheNamePM]
        int Field;
    }

    [AttributeMustSuffixTheNameST]
    public class FailedS
    {
        [AttributeMustSuffixTheNameSM]
        int Field;
    }

    [CorrectSecondaryOfInvalidPrimaryT]
    public class FailedPIndirect
    {
        [CorrectSecondaryOfInvalidPrimaryM]
        int Field;
    }

    [Test]
    public void Attribute_type_name_must_be_suffixed_with_Attribute()
    {
        var c = new GlobalTypeCache();
        var p = c.Get( typeof( FailedP ) );
        var s = c.Get( typeof( FailedS ) );
        using( TestHelper.Monitor.CollectTexts( out var logs ) )
        {
            p.TryGetInitializedAttributes( TestHelper.Monitor, out _ ).ShouldBeFalse();
            s.TryGetInitializedAttributes( TestHelper.Monitor, out _ ).ShouldBeFalse();
            p.DeclaredMembers.Single( m => m.Name == "Field" ).TryGetInitializedAttributes( TestHelper.Monitor, out _ ).ShouldBeFalse();
            s.DeclaredMembers.Single( m => m.Name == "Field" ).TryGetInitializedAttributes( TestHelper.Monitor, out _ ).ShouldBeFalse();
            logs.ShouldContain( """
                    Attribute name 'CK.Engine.TypeCollector.Tests.AttributeMustSuffixTheNamePT' is invalid: it must be suffixed by "Attribute".
                    """ )
                .ShouldContain( """
                    Attribute name 'CK.Engine.TypeCollector.Tests.AttributeMustSuffixTheNamePM' is invalid: it must be suffixed by "Attribute".
                    """ )
                .ShouldContain( """
                    Attribute name 'CK.Engine.TypeCollector.Tests.AttributeMustSuffixTheNameST' is invalid: it must be suffixed by "Attribute".
                    """ )
                .ShouldContain( """
                    Attribute name 'CK.Engine.TypeCollector.Tests.AttributeMustSuffixTheNameST' is invalid: it must be suffixed by "Attribute".
                    """ )
                .ShouldContain( """
                    Attribute name 'CK.Engine.TypeCollector.Tests.AttributeMustSuffixTheNameST' is invalid: it must be suffixed by "Attribute".
                    """ );
        }
        var i = c.Get( typeof( FailedPIndirect ) );
        using( TestHelper.Monitor.CollectTexts( out var logs ) )
        {
            i.TryGetInitializedAttributes( TestHelper.Monitor, out _ ).ShouldBeFalse();
            logs.ShouldContain( """
                    Attribute name 'CK.Engine.TypeCollector.Tests.AttributeMustSuffixTheNamePT' is invalid: it must be suffixed by "Attribute".
                    """ );
        }
        using( TestHelper.Monitor.CollectTexts( out var logs ) )
        {
            i.DeclaredMembers.Single( m => m.Name == "Field" ).TryGetInitializedAttributes( TestHelper.Monitor, out _ ).ShouldBeFalse();
            logs.ShouldContain( """
                    Attribute name 'CK.Engine.TypeCollector.Tests.AttributeMustSuffixTheNamePM' is invalid: it must be suffixed by "Attribute".
                    """ );
        }
        // The error is cached.
        using( TestHelper.Monitor.CollectTexts( out var logs ) )
        {
            p.TryGetInitializedAttributes( TestHelper.Monitor, out _ ).ShouldBeFalse();
            s.TryGetInitializedAttributes( TestHelper.Monitor, out _ ).ShouldBeFalse();
            p.DeclaredMembers.Single( m => m.Name == "Field" ).TryGetInitializedAttributes( TestHelper.Monitor, out _ ).ShouldBeFalse();
            s.DeclaredMembers.Single( m => m.Name == "Field" ).TryGetInitializedAttributes( TestHelper.Monitor, out _ ).ShouldBeFalse();
            i.TryGetInitializedAttributes( TestHelper.Monitor, out _ ).ShouldBeFalse();
            i.DeclaredMembers.Single( m => m.Name == "Field" ).TryGetInitializedAttributes( TestHelper.Monitor, out _ ).ShouldBeFalse();
            logs.ShouldNotContain( "Attribute name 'CK.Engine.TypeCollector.Tests.AttributeMustSuffixTheNamePT' is invalid: it must be suffixed by \"Attribute\"." )
                .ShouldNotContain( "Attribute name 'CK.Engine.TypeCollector.Tests.AttributeMustSuffixTheNamePM' is invalid: it must be suffixed by \"Attribute\"." )
                .ShouldNotContain( "Attribute name 'CK.Engine.TypeCollector.Tests.AttributeMustSuffixTheNameST' is invalid: it must be suffixed by \"Attribute\"." )
                .ShouldNotContain( "Attribute name 'CK.Engine.TypeCollector.Tests.AttributeMustSuffixTheNameST' is invalid: it must be suffixed by \"Attribute\"." );
        }
    }

    [SomePrimaryType( "Demo" )]
    [SomeSecondaryType( "Demo-1" )]
    [SomeSecondaryType( "Demo-2" )]
    [AnotherPrimaryType( "AnotherDemo" )]
    [AnotherSecondaryType( "AnotherDemo-1" )]
    [AnotherSecondaryType( "AnotherDemo-2" )]
    [SomeUnrelatedPrimaryType( "DemoUnrelated" )]
    public interface SomeInterface
    {
        [SomePrimaryMember( "Demo" )]
        [SomeSecondaryMember( "Demo-1" )]
        [SomeSecondaryMember( "Demo-2" )]
        [SomeSecondaryMember( "Demo-3" )]
        string DoSomething( int value );
    }

    // Doesn't compile: AnotherPrimaryType has AttributeUsage( AttributeTargets.Interface ).
    // 
    //  [AnotherPrimaryType( "AnotherDemo" )]
    //  public class SomeClass
    //  {
    //  }


    [Test]
    public void TryGetInitializedAttributes_on_type()
    {
        var c = new GlobalTypeCache();
        var t = c.Get( typeof( SomeInterface ) );
        t.TryGetInitializedAttributes( TestHelper.Monitor, out var attributes ).ShouldBeTrue();
        attributes.Length.ShouldBe( 8 );
        attributes[0].ShouldBeAssignableTo<System.Runtime.CompilerServices.NullableContextAttribute>();
        attributes.Skip( 1 )
                  .Select( a => a.GetType().Name )
                  .ShouldBe( ["SomePrimaryTypeAttributeImpl",
                              "SomeSecondaryTypeAttributeImpl",
                              "SomeSecondaryTypeAttributeImpl",
                              "AnotherPrimaryTypeAttributeImpl",
                              "AnotherSecondaryTypeAttributeImpl",
                              "AnotherSecondaryTypeAttributeImpl",
                              "SomeUnrelatedPrimaryTypeAttributeImpl"] );

        // Attribute declarations ordering is preserved.
        attributes.OfType<IAttributeHasNameProperty>().Select( a => a.TheAttributeName )
          .ShouldBe( ["Demo",
                      "Demo-1",
                      "Demo-2",
                      "AnotherDemo",
                      "AnotherDemo-1",
                      "AnotherDemo-2",
                      "DemoUnrelated"
                   ] );

    }

    [Test]
    public void TryGetInitializedAttributes_on_member()
    {
        var c = new GlobalTypeCache();
        var t = c.Get( typeof( SomeInterface ) );
        var method = t.DeclaredMembers.Single( m => m.Name == "DoSomething" );
        method.TryGetInitializedAttributes( TestHelper.Monitor, out var attributes ).ShouldBeTrue();
        attributes.Length.ShouldBe( 4 );
        attributes.Select( a => a.GetType().Name )
                  .ShouldBe( ["SomePrimaryMemberAttributeImpl",
                              "SomeSecondaryMemberAttributeImpl",
                              "SomeSecondaryMemberAttributeImpl",
                              "SomeSecondaryMemberAttributeImpl"] );

        // Attribute declarations ordering is preserved.
        attributes.OfType<IAttributeHasNameProperty>().Select( a => a.TheAttributeName )
          .ShouldBe( ["Demo",
                      "Demo-1",
                      "Demo-2",
                      "Demo-3"
                   ] );

    }

    [Test]
    public void OnInitialized_exceptions_are_caught()
    {
        SomeUnrelatedPrimaryTypeAttribute.ImplInitializationThrow = false;
        SomeUnrelatedPrimaryTypeAttribute.ImplOnInitializedThrow = true;
        try
        {
            var c = new GlobalTypeCache();
            var t = c.Get( typeof( SomeInterface ) );
            using( TestHelper.Monitor.CollectTexts( out var logs ) )
            {
                t.TryGetInitializedAttributes( TestHelper.Monitor, out var attributes )
                    .ShouldBeFalse();

                logs.ShouldContain( "While calling 'CK.Engine.TypeCollector.Tests.SomeUnrelatedPrimaryTypeAttributeImpl.OnInitialized' from CK.Engine.TypeCollector.Tests.PrimaryAndSecondaryAttributeTests.SomeInterface." )
                    .ShouldNotContain( "While initializing 'CK.Engine.TypeCollector.Tests.SomeUnrelatedPrimaryTypeAttributeImpl' from CK.Engine.TypeCollector.Tests.PrimaryAndSecondaryAttributeTests.SomeInterface." );
            }
        }
        finally
        {
            SomeUnrelatedPrimaryTypeAttribute.ImplOnInitializedThrow = false;
        }
    }

    [Test]
    public void Initialize_exceptions_are_caught_and_OnInitialized_is_not_called()
    {
        SomeUnrelatedPrimaryTypeAttribute.ImplInitializationThrow = true;
        SomeUnrelatedPrimaryTypeAttribute.ImplOnInitializedThrow = true;
        try
        {
            var c = new GlobalTypeCache();
            var t = c.Get( typeof( SomeInterface ) );
            using( TestHelper.Monitor.CollectTexts( out var logs ) )
            {
                t.TryGetInitializedAttributes( TestHelper.Monitor, out var attributes )
                    .ShouldBeFalse();

                logs.ShouldContain( "While initializing 'CK.Engine.TypeCollector.Tests.SomeUnrelatedPrimaryTypeAttributeImpl' from CK.Engine.TypeCollector.Tests.PrimaryAndSecondaryAttributeTests.SomeInterface." )
                    .ShouldNotContain( "While calling 'CK.Engine.TypeCollector.Tests.SomeUnrelatedPrimaryTypeAttributeImpl.OnInitialized' from CK.Engine.TypeCollector.Tests.PrimaryAndSecondaryAttributeTests.SomeInterface." );
            }
        }
        finally
        {
            SomeUnrelatedPrimaryTypeAttribute.ImplInitializationThrow = false;
            SomeUnrelatedPrimaryTypeAttribute.ImplOnInitializedThrow = false;
        }
    }

    [SomeSecondaryType( "Demo-1" )]
    [SomeSecondaryType( "Demo-2" )]
    public class MissingPrimary1
    {
    }

    [Test]
    public void Secondary_missing_primary_error_details_1()
    {
        var c = new GlobalTypeCache();
        var t = c.Get( typeof( MissingPrimary1 ) );
        using( TestHelper.Monitor.CollectTexts( out var logs ) )
        {
            t.TryGetInitializedAttributes( TestHelper.Monitor, out var attributes )
                .ShouldBeFalse();

            logs.ShouldContain( """
                **[SomeSecondaryType]** Missing a [SomePrimaryType] primary attribute (or a specialization) above this one.
                **[SomeSecondaryType]** (Same as above.)
                CK.Engine.TypeCollector.Tests.PrimaryAndSecondaryAttributeTests.MissingPrimary1
                """ );
        }
    }

    [SomeSecondaryType( "Demo-1" )]
    [SomeSecondaryType( "Demo-2" )]
    [AnotherSecondaryType( "Demo-2" )]
    public interface MissingPrimary2
    {
    }

    [Test]
    public void Secondary_missing_primary_error_details_2()
    {
        var c = new GlobalTypeCache();
        var t = c.Get( typeof( MissingPrimary2 ) );
        using( TestHelper.Monitor.CollectTexts( out var logs ) )
        {
            t.TryGetInitializedAttributes( TestHelper.Monitor, out var attributes )
                .ShouldBeFalse();

            logs.ShouldContain( """
                **[SomeSecondaryType]** Missing a [SomePrimaryType] primary attribute (or a specialization) above this one.
                **[SomeSecondaryType]** (Same as above.)
                **[AnotherSecondaryType]** Missing a [AnotherPrimaryType] primary attribute (or a specialization) above this one.
                CK.Engine.TypeCollector.Tests.PrimaryAndSecondaryAttributeTests.MissingPrimary2
                """ );
        }
    }

    [SomePrimaryType( "Demo" )]
    [SomeSecondaryType( "Demo-1" )]
    [SomeSecondaryType( "Demo-2" )]
    [AnotherSecondaryType( "Demo-2" )]
    public interface MissingPrimary3
    {
    }

    [Test]
    public void Secondary_missing_primary_error_details_3()
    {
        var c = new GlobalTypeCache();
        var t = c.Get( typeof( MissingPrimary3 ) );
        using( TestHelper.Monitor.CollectTexts( out var logs ) )
        {
            t.TryGetInitializedAttributes( TestHelper.Monitor, out var attributes )
                .ShouldBeFalse();

            logs.ShouldContain( """
                [SomePrimaryType] (primary n°1)
                [SomeSecondaryType]
                [SomeSecondaryType]
                **[AnotherSecondaryType]** Missing a [AnotherPrimaryType] primary attribute (or a specialization) above this one.
                CK.Engine.TypeCollector.Tests.PrimaryAndSecondaryAttributeTests.MissingPrimary3
                """ );
        }
    }

    [SomeSecondaryType( "Demo-1" )]
    [SomeSecondaryType( "Demo-2" )]
    [SomePrimaryType( "Demo" )]
    [AnotherSecondaryType( "AnotherDemo-1" )]
    [AnotherPrimaryType( "AnotherDemo" )]
    public interface BadOrder1
    {
    }

    [Test]
    public void Secondary_bad_order_error_details_1()
    {
        var c = new GlobalTypeCache();
        var t = c.Get( typeof( BadOrder1 ) );
        using( TestHelper.Monitor.CollectTexts( out var logs ) )
        {
            t.TryGetInitializedAttributes( TestHelper.Monitor, out var attributes )
                .ShouldBeFalse();

            logs.ShouldContain( """
                **[SomeSecondaryType]** Must be after [SomePrimaryType] (n°1).
                **[SomeSecondaryType]** (Same as above.)
                [SomePrimaryType] (primary n°1)
                **[AnotherSecondaryType]** Must be after [AnotherPrimaryType] (n°2).
                [AnotherPrimaryType] (primary n°2)
                CK.Engine.TypeCollector.Tests.PrimaryAndSecondaryAttributeTests.BadOrder1
                """ );
        }
    }

    [SomePrimaryType( "Demo" )]
    [SomeSecondaryType( "Demo-1" )]
    [SomeSecondaryType( "Demo-2" )]
    [AnotherSecondaryType( "AnotherDemo-1" )]
    [AnotherPrimaryType( "AnotherDemo" )]
    public interface BadOrder2
    {
    }

    [Test]
    public void Secondary_bad_order_error_details_2()
    {
        var c = new GlobalTypeCache();
        var t = c.Get( typeof( BadOrder2 ) );
        using( TestHelper.Monitor.CollectTexts( out var logs ) )
        {
            t.TryGetInitializedAttributes( TestHelper.Monitor, out var attributes )
                .ShouldBeFalse();

            logs.ShouldContain( """
                [SomePrimaryType] (primary n°1)
                [SomeSecondaryType]
                [SomeSecondaryType]
                **[AnotherSecondaryType]** Must be after [AnotherPrimaryType] (n°2).
                [AnotherPrimaryType] (primary n°2)
                CK.Engine.TypeCollector.Tests.PrimaryAndSecondaryAttributeTests.BadOrder2
                """ );
        }
    }

    public class BindingDemo
    {
        [SomePrimaryMemberSpecAttribute( "Demo", SomePrimaryMemberSpecAttribute.ImplBinding.SameAsBase, 3712 )]
        [SomeSecondaryMember( "Demo-1" )]
        public void UsingBaseImpl() { }

        [SomePrimaryMemberSpecAttribute( "Demo", SomePrimaryMemberSpecAttribute.ImplBinding.SpecializedImpl, 3712 )]
        [SomeSecondaryMember( "Demo-1" )]
        public void UsingSpecializedImpl() { }

        [SomePrimaryMemberSpecAttribute( "Demo", SomePrimaryMemberSpecAttribute.ImplBinding.DifferentImpl, 3712 )]
        [SomeSecondaryMember( "Demo-1" )]
        public void UsingDifferentImpl() { }
    }

    [TestCase( "UsingBaseImpl" )]
    [TestCase( "UsingSpecializedImpl" )]
    [TestCase( "UsingDifferentImpl" )]
    public void Impl_binding_and_specialization( string binding )
    {
        var c = new GlobalTypeCache();
        var t = c.Get( typeof( BindingDemo ) );
        var method = t.DeclaredMembers.Single( m => m.Name == binding );
        method.TryGetInitializedAttributes( TestHelper.Monitor, out var attributes ).ShouldBeTrue();

        attributes.Length.ShouldBe( 2 );
        var primary = attributes[0].ShouldBeAssignableTo<PrimaryMemberAttributeImpl>();
        primary.SecondaryCount.ShouldBe( 1 );
        primary.SecondaryAttributes.Single().ShouldBeAssignableTo<SecondaryMemberAttributeImpl>();

        var action = attributes.OfType<ISomePrimaryMemberSpecBehavior>()
                               .Single()
                               .DoSomethingWithTheSpecAttribute();
        var expectedAction = binding switch
        {
            "UsingBaseImpl" => "My Attribute is a Spec and it has 3712.",
            "UsingSpecializedImpl" => "I'm the [Spec] implementation, I can do stuff with 3712.",
            _ => "Totally independent [Spec] implementation can do anything with 3712."
        };
        action.ShouldBe( expectedAction );
    }
}
