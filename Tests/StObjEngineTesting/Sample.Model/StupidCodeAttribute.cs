using CK.Setup;
using System;

namespace Sample.Model;

/// <summary>
/// Stupid attribute that defines the code of a method that must be abstract or virtual.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class StupidCodeAttribute : ContextBoundDelegationAttribute
{
    public StupidCodeAttribute( string actualCode )
        : base( "Sample.Engine.StupidCodeAttributeImpl, Sample.Engine" )
    {
        ActualCode = actualCode;
    }

    public bool IsLambda { get; set; }

    public string ActualCode { get; }
}
