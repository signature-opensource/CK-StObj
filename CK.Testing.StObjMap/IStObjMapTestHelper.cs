namespace CK.Testing
{
    /// <summary>
    /// Mixin of <see cref="StObjMap.IStObjMapTestHelperCore"/> and <see cref="IMonitorTestHelper"/>.
    /// </summary>
    public interface IStObjMapTestHelper : IMixinTestHelper, StObjMap.IStObjMapTestHelperCore, IMonitorTestHelper
    {
    }
}
