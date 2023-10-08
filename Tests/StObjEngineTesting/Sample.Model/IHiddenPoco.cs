namespace Sample.Model
{
    namespace InternalAndDuckTyped
    {
        class ExcludeCKTypeAttribute : System.Attribute { }
    }

    /// <summary>
    /// Hidden IPoco by [ExcludeCKType] defined internally in this assembly.
    /// </summary>
    [InternalAndDuckTyped.ExcludeCKType]
    public interface IHiddenPoco : CK.Core.IPoco
    {
    }

}
