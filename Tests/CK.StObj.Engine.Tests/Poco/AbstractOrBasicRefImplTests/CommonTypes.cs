using CK.Core;

namespace CK.StObj.Engine.Tests.Poco.AbstractImplTests
{
    public class CommonTypes
    {
        [CKTypeDefiner]
        public interface IAbstractBase : IPoco { }

        [CKTypeDefiner]
        public interface IAbstract1 : IAbstractBase { }

        [CKTypeDefiner]
        public interface IAbstract1Closed : IClosedPoco, IAbstract1 { }

        [CKTypeDefiner]
        public interface IAbstract2 : IAbstractBase { }

        public interface IVerySimplePoco : IPoco
        {
            int Value { get; set; }
        }

        public interface ISecondaryVerySimplePoco : IVerySimplePoco, IAbstract1
        {
        }

        public interface IOtherSecondaryVerySimplePoco : IVerySimplePoco, IAbstract2
        {
        }

        public interface IClosed : IAbstract1Closed
        {
            int Value { get; set; }
        }

    }
}
