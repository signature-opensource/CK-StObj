using CK.Core;
using System;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Internal: unifies <see cref="PrimaryTypeAttributeImpl"/> and <see cref="PrimaryMemberAttributeImpl"/>.
/// </summary>
interface IPrimaryAttributeImpl : IAttributeImpl
{
    int SecondaryCount { get; }

    internal Attribute Attribute { get; }

    internal bool Initialize( IActivityMonitor monitor );
}
