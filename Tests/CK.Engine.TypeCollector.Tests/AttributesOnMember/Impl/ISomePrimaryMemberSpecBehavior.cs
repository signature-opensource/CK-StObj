using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Engine.TypeCollector.Tests;

/// <summary>
/// Provides a common behavior to all the implementation of the <see cref="SomePrimaryMemberSpecAttribute"/>.
/// </summary>
public interface ISomePrimaryMemberSpecBehavior
{
    string DoSomethingWithTheSpecAttribute();
}
