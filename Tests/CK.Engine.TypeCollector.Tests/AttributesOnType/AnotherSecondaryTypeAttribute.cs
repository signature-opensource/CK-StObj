using CK.Core;
using System;

namespace CK.Engine.TypeCollector.Tests;

[AttributeUsage( AttributeTargets.Interface, AllowMultiple = true, Inherited = false )]
public class AnotherSecondaryTypeAttribute : SecondaryTypeAttribute<AnotherPrimaryTypeAttribute>
{
    public AnotherSecondaryTypeAttribute( string name )
        : base( "CK.Engine.TypeCollector.Tests.AnotherSecondaryTypeAttributeImpl, CK.Engine.TypeCollector.Tests" )
    {
        Name = name;
    }

    public string Name { get; }
}
