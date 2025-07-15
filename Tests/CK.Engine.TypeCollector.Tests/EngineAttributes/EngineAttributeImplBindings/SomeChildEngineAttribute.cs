using CK.Core;
using System;

namespace CK.Engine.TypeCollector.Tests;

[AttributeUsage( SomeEngineAttribute.Targets, AllowMultiple = true, Inherited = false )]
public class SomeChildEngineAttribute : EngineAttribute<SomeEngineAttribute>
{
    public SomeChildEngineAttribute( string name )
        : base( "CK.Engine.TypeCollector.Tests.SomeChildEngineAttributeImpl, CK.Engine.TypeCollector.Tests" )
    {
        Name = name;
    }

    public string Name { get; }
}
