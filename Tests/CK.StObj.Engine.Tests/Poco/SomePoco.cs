using CK.Core;

namespace CK.StObj.Engine.Tests.Poco
{

    public interface IBasicPoco : IPoco
    {
        int BasicProperty { get; set; }
    }

    public interface IEBasicPoco : IBasicPoco
    {
        int ExtendProperty { get; set; }
    }

    public interface IEBasicPocoWithReadOnly : IEBasicPoco
    {
        int ReadOnlyProperty { get; }
    }

    public interface IEAlternateBasicPoco : IBasicPoco
    {
        int AlternateProperty { get; set; }
    }

    public interface IEExtraBasicPoco : IBasicPoco
    {
        int ExtraProperty { get; set; }
    }

    public interface IEIndependentBasicPoco : IBasicPoco
    {
        int IndependentProperty { get; set; }
    }

    public interface IECombineBasicPoco : IEAlternateBasicPoco, IEExtraBasicPoco
    {
    }

}
