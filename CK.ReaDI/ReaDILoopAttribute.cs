using System;

namespace CK.Core;

[AttributeUsage( AttributeTargets.Parameter, AllowMultiple = false, Inherited = false )]
public sealed class ReaDILoopAttribute : Attribute
{
}
