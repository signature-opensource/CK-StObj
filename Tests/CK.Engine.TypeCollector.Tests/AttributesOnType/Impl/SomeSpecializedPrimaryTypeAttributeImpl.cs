namespace CK.Engine.TypeCollector.Tests;

/// <summary>
/// Here, the attribute implementation specializes the implementation of its base but this
/// is not required: the attribute implementation of a specialized attribute can be totally
/// independent of the one of its base. The 2 hierarchies can differ and this is intended.
/// </summary>
public class SomeSpecializedPrimaryTypeAttributeImpl : SomePrimaryTypeAttributeImpl
{
}
