using CK.Core;
using System;

namespace CK.Engine.TypeCollector.Tests;

public class SomePrimaryTypeAttribute : PrimaryTypeAttribute
{
    public SomePrimaryTypeAttribute( string name )
        : base( "CK.Engine.TypeCollector.Tests.SomePrimaryTypeAttributeImpl, CK.Engine.TypeCollector.Tests" )
    {
        Name = name;
    }

    public string Name { get; init; }
}
