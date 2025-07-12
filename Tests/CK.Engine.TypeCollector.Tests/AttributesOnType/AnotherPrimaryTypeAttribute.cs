using CK.Core;
using System;

namespace CK.Engine.TypeCollector.Tests;

/// <summary>
/// This attributes redefine the AttributeUsage that comes from the PrimaryTypeAttribute
/// to be more restrictive (it can only decorate interfaces).
/// But this doesn't prevent other primary attributes (like <see cref="SomePrimaryTypeAttribute"/>) to be defined.
/// </summary>
[AttributeUsage( AttributeTargets.Interface, AllowMultiple = false, Inherited = false )]
public class AnotherPrimaryTypeAttribute : PrimaryTypeAttribute
{
    public AnotherPrimaryTypeAttribute( string name )
        : base( "CK.Engine.TypeCollector.Tests.AnotherPrimaryTypeAttributeImpl, CK.Engine.TypeCollector.Tests" )
    {
        Name = name;
    }

    public string Name { get; }
}

