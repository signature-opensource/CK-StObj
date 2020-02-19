namespace CK.Testing
{
    /// <summary>
    /// Mixin that supports StObj engine during setup.
    /// </summary>
    public interface IStObjSetupTestHelper : IMixinTestHelper, ICKSetupTestHelper, IStObjMapTestHelper, StObjSetup.IStObjSetupTestHelperCore
    {
    }
}
