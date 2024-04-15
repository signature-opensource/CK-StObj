using CK.Core;
using CK.Setup;
using FluentAssertions;
using NUnit.Framework;
using System;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Service.TypeCollector
{
    [TestFixture]
    public class CKTypeKindDetectorTests
    {
        public class Nop { }

        public class Obj : IRealObject { }

        public class Serv : IAutoService { }

        public class Scoped : IScopedAutoService { }

        public class Singleton : ISingletonAutoService { }

        [Test]
        public void basic_type_detection()
        {
            var a = new CKTypeKindDetector();
            //a.GetValidKind( TestHelper.Monitor, typeof( Nop ) ).Should().Be( CKTypeKind.None );
            a.GetValidKind( TestHelper.Monitor, typeof( Obj ) ).Should().Be( CKTypeKind.RealObject );
            a.GetValidKind( TestHelper.Monitor, typeof( Serv ) ).Should().Be( CKTypeKind.IsAutoService );
            a.GetValidKind( TestHelper.Monitor, typeof( Scoped ) ).Should().Be( CKTypeKind.IsAutoService | CKTypeKind.IsScoped );
            a.GetValidKind( TestHelper.Monitor, typeof( Singleton ) ).Should().Be( CKTypeKind.IsAutoService | CKTypeKind.IsSingleton );
        }

        public class SpecObj : Obj { }
        public class SpecServ : Serv { }
        public class SpecScoped : Scoped { }
        public class SpecSingleton : Singleton { }

        [Test]
        public void specialized_type_detection()
        {
            var a = new CKTypeKindDetector();
            a.GetValidKind( TestHelper.Monitor, typeof( SpecObj ) ).Should().Be( CKTypeKind.RealObject );
            a.GetValidKind( TestHelper.Monitor, typeof( SpecServ ) ).Should().Be( CKTypeKind.IsAutoService );
            a.GetValidKind( TestHelper.Monitor, typeof( SpecScoped ) ).Should().Be( CKTypeKind.IsAutoService | CKTypeKind.IsScoped );
            a.GetValidKind( TestHelper.Monitor, typeof( SpecSingleton ) ).Should().Be( CKTypeKind.IsAutoService | CKTypeKind.IsSingleton );
        }

        [CKTypeDefiner] public class ObjDefiner : IRealObject { }
        [CKTypeDefiner] public class ServDefiner : IAutoService { }
        [CKTypeDefiner] public class ScopedDefiner : IScopedAutoService { }
        [CKTypeDefiner] public class SingletonDefiner : ISingletonAutoService { }

        [Test]
        public void Definers_are_marked_with_CKTypeDefiner_and_are_not_AutoServices_or_RealObjects()
        {
            var a = new CKTypeKindDetector();
            a.GetValidKind( TestHelper.Monitor, typeof( ObjDefiner ) ).Should().Be( CKTypeKind.None );
            a.GetValidKind( TestHelper.Monitor, typeof( ServDefiner ) ).Should().Be( CKTypeKind.None );
            a.GetValidKind( TestHelper.Monitor, typeof( ScopedDefiner ) ).Should().Be( CKTypeKind.None );
            a.GetValidKind( TestHelper.Monitor, typeof( SingletonDefiner ) ).Should().Be( CKTypeKind.None );
        }

        public class SpecObjDefiner : ObjDefiner { }
        public class SpecServDefiner : ServDefiner { }
        public class SpecScopedDefiner : ScopedDefiner { }
        public class SpecSingletonDefiner : SingletonDefiner { }

        [Test]
        public void specialization_of_Definers_are_cktype()
        {
            var a = new CKTypeKindDetector();
            a.GetValidKind( TestHelper.Monitor, typeof( SpecObjDefiner ) ).Should().Be( CKTypeKind.RealObject );
            a.GetValidKind( TestHelper.Monitor, typeof( SpecServDefiner ) ).Should().Be( CKTypeKind.IsAutoService );
            a.GetValidKind( TestHelper.Monitor, typeof( SpecScopedDefiner ) ).Should().Be( CKTypeKind.IsAutoService | CKTypeKind.IsScoped );
            a.GetValidKind( TestHelper.Monitor, typeof( SpecSingletonDefiner ) ).Should().Be( CKTypeKind.IsAutoService | CKTypeKind.IsSingleton );
        }


        [CKTypeDefiner] public class ObjDefinerLevel2 : ObjDefiner { }
        [CKTypeDefiner] public class ServDefinerLevel2 : ServDefiner { }
        [CKTypeDefiner] public class ScopedDefinerLevel2 : ScopedDefiner { }
        [CKTypeDefiner] public class SingletonDefinerLevel2 : SingletonDefiner { }

        [Test]
        public void Definers_can_be_specialized_as_another_layer_of_Definers_and_are_still_abstract()
        {
            var a = new CKTypeKindDetector();
            a.GetValidKind( TestHelper.Monitor, typeof( ObjDefinerLevel2 ) ).Should().Be( CKTypeKind.None );
            a.GetValidKind( TestHelper.Monitor, typeof( ServDefinerLevel2 ) ).Should().Be( CKTypeKind.None );
            a.GetValidKind( TestHelper.Monitor, typeof( ScopedDefinerLevel2 ) ).Should().Be( CKTypeKind.None );
            a.GetValidKind( TestHelper.Monitor, typeof( SingletonDefinerLevel2 ) ).Should().Be( CKTypeKind.None );
        }

        public class SpecObjDefinerLevel2 : ObjDefinerLevel2 { }
        public class SpecServDefinerLevel2 : ServDefinerLevel2 { }
        public class SpecScopedDefinerLevel2 : ScopedDefinerLevel2 { }
        public class SpecSingletonDefinerLevel2 : SingletonDefinerLevel2 { }

        [Test]
        public void specialization_of_DefinersLevel2_are_cktype()
        {
            var a = new CKTypeKindDetector();
            a.GetValidKind( TestHelper.Monitor, typeof( SpecObjDefinerLevel2 ) ).Should().Be( CKTypeKind.RealObject );
            a.GetValidKind( TestHelper.Monitor, typeof( SpecServDefinerLevel2 ) ).Should().Be( CKTypeKind.IsAutoService );
            a.GetValidKind( TestHelper.Monitor, typeof( SpecScopedDefinerLevel2 ) ).Should().Be( CKTypeKind.IsAutoService | CKTypeKind.IsScoped );
            a.GetValidKind( TestHelper.Monitor, typeof( SpecSingletonDefinerLevel2 ) ).Should().Be( CKTypeKind.IsAutoService | CKTypeKind.IsSingleton );
        }

        /// <summary>
        /// Interfaces cannot be IRealObject and IAutoService but classes can.
        /// </summary>
        public interface INotPossible0 : IRealObject, IAutoService { }
        public interface INotPossible1 : IScopedAutoService, ISingletonAutoService { }
        public interface INotPossible2 : IScopedAutoService, IPoco { }
        public interface INotPossible3 : IRealObject, IPoco { }
        public interface INotPossible4 : IAutoService, IPoco { }


        public class NotPossible0 : ScopedDefiner, IRealObject { }
        public class NotPossible1 : ScopedDefinerLevel2, IRealObject { }
        public class NotPossible2 : IPoco { }

        [Test]
        public void conflict_detection()
        {
            var a = new CKTypeKindDetector();

            void CheckNotPossible( System.Type t )
            {
                bool hasCombinationError = false;
                bool hasRegistrationError = false;
                using( TestHelper.Monitor.OnError( () => hasRegistrationError = true ) )
                {
                    var k = a.GetRawKind( TestHelper.Monitor, t );
                    hasCombinationError = k.GetCombinationError( t.IsClass ) != null;
                }
                (hasCombinationError | hasRegistrationError).Should().BeTrue();
            }

            CheckNotPossible( typeof( INotPossible0 ) );
            CheckNotPossible( typeof( INotPossible1 ) );
            CheckNotPossible( typeof( INotPossible2 ) );
            CheckNotPossible( typeof( INotPossible3 ) );
            CheckNotPossible( typeof( INotPossible4 ) );
            CheckNotPossible( typeof( NotPossible0 ) );
            CheckNotPossible( typeof( NotPossible1 ) );
            CheckNotPossible( typeof( NotPossible2 ) );

            // This is explicitly allowed thanks to the parameter.
            a.GetRawKind( TestHelper.Monitor, typeof( INotPossible0 ) ).GetCombinationError( isClass: true ).Should().BeNull( "This is possible for a Class." );
            a.GetRawKind( TestHelper.Monitor, typeof( INotPossible0 ) ).GetCombinationError( isClass: false ).Should().NotBeNull();
        }

        // IRA is a Super Definer.
        [CKTypeSuperDefiner]
        public interface IRA : IRealObject { }

        // IRB is a  Definer, not a Real Object.
        public interface IRB : IRA { }

        // Interface IRC is a Real Object.
        public interface IRC : IRB { }

        // Class RD that implements the IRC real object is obviously a Real Object.
        public class RD : IRC { }

        // Class RC is a Real Object (IRB is a Definer).
        public class RC : IRB { }

        // Class RB is a NOT Real Object: IRA is a Super Definer.
        public class RB : IRA { }

        // Class RBC is a Real Object: RB is a Definer.
        public class RBC : RB { }

        // Class RBBase is another Definer (IRB is a also Definer).
        [CKTypeDefiner]
        public class RBBase : IRB { }

        // RBBaseC is eventually a real object.
        public class RBBaseC : RBBase { }

        // Class RBSuperBase is a Super Definer (IRB is a also Definer).
        [CKTypeSuperDefiner]
        public class RBSuperBase : IRB { }

        // As its name states...
        public class RBStillDefiner : RBSuperBase { }

        // As its name states...
        public class RBAtLast : RBStillDefiner { }

        // As its name states...
        public class RBOfCourse : RBStillDefiner { }

        [Test]
        public void Definer_and_Super_Definer_on_RealObject()
        {
            var a = new CKTypeKindDetector();
            a.GetValidKind( TestHelper.Monitor, typeof( IRA ) ).Should().Be( CKTypeKind.None );
            a.GetValidKind( TestHelper.Monitor, typeof( IRB ) ).Should().Be( CKTypeKind.None );
            a.GetValidKind( TestHelper.Monitor, typeof( IRC ) ).Should().Be( CKTypeKind.RealObject );
            a.GetValidKind( TestHelper.Monitor, typeof( RD ) ).Should().Be( CKTypeKind.RealObject );
            a.GetValidKind( TestHelper.Monitor, typeof( RC ) ).Should().Be( CKTypeKind.RealObject );
            a.GetValidKind( TestHelper.Monitor, typeof( RB ) ).Should().Be( CKTypeKind.None );
            a.GetValidKind( TestHelper.Monitor, typeof( RBC ) ).Should().Be( CKTypeKind.RealObject );
            a.GetValidKind( TestHelper.Monitor, typeof( RBBase ) ).Should().Be( CKTypeKind.None );
            a.GetValidKind( TestHelper.Monitor, typeof( RBBaseC ) ).Should().Be( CKTypeKind.RealObject );
            a.GetValidKind( TestHelper.Monitor, typeof( RBSuperBase ) ).Should().Be( CKTypeKind.None );
            a.GetValidKind( TestHelper.Monitor, typeof( RBStillDefiner ) ).Should().Be( CKTypeKind.None );
            a.GetValidKind( TestHelper.Monitor, typeof( RBAtLast ) ).Should().Be( CKTypeKind.RealObject );
            a.GetValidKind( TestHelper.Monitor, typeof( RBOfCourse ) ).Should().Be( CKTypeKind.RealObject );
        }


        // The base Options interface is Singleton: you use it when you want to
        // inject immutable options in any service that may need it (be it scoped or singleton).
        // Unfortunately, sometimes, you want these options to be able to change while the app is running: the IOptionsSnapshot
        // is a Scoped service that will contain the options value at the start of the request and it will be the same during the lifetime
        // of the request (which is a good thing).
        // But, here it is: the base is Singleton and the specialization is Scoped!
        public interface IOptions<out TOptions> where TOptions : class, new()
        {
            TOptions Value { get; }
        }

        public interface IOptionsSnapshot<out TOptions> : IOptions<TOptions> where TOptions : class, new()
        {
            TOptions Get( string name );
        }

        [Test]
        public void preliminary_SetAutoServiceKind_ordering_matters()
        {
            {
                var a = new CKTypeKindDetector();
                bool success = true;
                using( TestHelper.Monitor.OnError( () => success = false ) )
                {
                    a.SetAutoServiceKind( TestHelper.Monitor, typeof( IOptions<> ), AutoServiceKind.IsSingleton | AutoServiceKind.IsProcessService );
                    a.SetAutoServiceKind( TestHelper.Monitor, typeof( IOptionsSnapshot<> ), AutoServiceKind.IsScoped | AutoServiceKind.IsProcessService );
                }
                success.Should().BeFalse( "From general to specific: this fails!" );
            }
            {
                var a = new CKTypeKindDetector();
                bool success = true;
                using( TestHelper.Monitor.OnError( () => success = false ) )
                {
                    a.SetAutoServiceKind( TestHelper.Monitor, typeof( IOptionsSnapshot<> ), AutoServiceKind.IsScoped | AutoServiceKind.IsProcessService );
                    a.SetAutoServiceKind( TestHelper.Monitor, typeof( IOptions<> ), AutoServiceKind.IsSingleton | AutoServiceKind.IsProcessService );
                }
                success.Should().BeTrue( "From specific to general: success!" );

                var baseO = a.GetValidKind( TestHelper.Monitor, typeof( IOptions<> ) );
                var specO = a.GetValidKind( TestHelper.Monitor, typeof( IOptionsSnapshot<> ) );
                baseO.ToStringClear( false ).Should().Be( "IsSingleton|IsProcessService" );
                specO.ToStringClear( false ).Should().Be( "IsScopedService|IsProcessService" );
            }
        }

        public class Opt : IOptions<object>
        {
            public object Value => throw new NotImplementedException();
        }

        public class OptS : IOptionsSnapshot<object>
        {
            public object Value => throw new NotImplementedException();

            public object Get( string name )
            {
                throw new NotImplementedException();
            }
        }

        [Test]
        public void generic_type_definition_takes_precedence_over_inheritance()
        {
            var a = new CKTypeKindDetector();
            bool success = true;
            using( TestHelper.Monitor.OnError( () => success = false ) )
            {
                a.SetAutoServiceKind( TestHelper.Monitor, typeof( IOptionsSnapshot<> ), AutoServiceKind.IsScoped | AutoServiceKind.IsProcessService );
                a.SetAutoServiceKind( TestHelper.Monitor, typeof( IOptions<> ), AutoServiceKind.IsSingleton | AutoServiceKind.IsProcessService );
            }
            success.Should().BeTrue();

            a.GetValidKind( TestHelper.Monitor, typeof( IOptions<object> ) ).ToStringClear( false ).Should().Be( "IsSingleton|IsProcessService" );
            a.GetValidKind( TestHelper.Monitor, typeof( IOptionsSnapshot<object> ) ).ToStringClear( false ).Should().Be( "IsScopedService|IsProcessService" );

            a.GetValidKind( TestHelper.Monitor, typeof( Opt ) ).ToStringClear( false ).Should().Be( "IsSingleton|IsProcessService" );
            a.GetValidKind( TestHelper.Monitor, typeof( OptS ) ).ToStringClear( false ).Should().Be( "IsScopedService|IsProcessService" );
        }

        public class Base<T, U> { }

        public class G<T> { }

        public class Derived<V> : Base<string, V>
        {
            public G<Derived<V>>? F;
        }

        [Test]
        public void semi_open_generic_definition()
        {
            var cache = new CKTypeKindDetector();
            var closed = cache.GetCachedType( typeof( Derived<int> ) );

        }
    }
}
