using CK.Core;
using System;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Internal: unifies all attribute implementation.
/// </summary>
interface IAttributeImpl
{
    internal bool InitFields( IActivityMonitor monitor, ICachedItem item, Attribute attribute );

    internal ReadOnlySpan<char> AttributeName { get; }

    internal bool OnInitialized( IActivityMonitor monitor );
}
