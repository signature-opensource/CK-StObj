using CK.Core;
using System;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Internal: unifies <see cref="SecondaryTypeAttributeImpl"/> and <see cref="SecondaryMemberAttributeImpl"/>.
/// </summary>
interface ISecondaryAttributeImpl : IAttributeImpl
{
    internal bool SetPrimary( IActivityMonitor monitor, IPrimaryAttributeImpl primary, ISecondaryAttributeImpl? lastSecondary );

    internal Type ExpectedPrimaryType { get; }

    internal IPrimaryAttributeImpl? Primary { get; }
}
