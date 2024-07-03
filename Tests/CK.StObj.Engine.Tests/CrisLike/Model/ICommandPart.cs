using CK.Core;
using System;
using static CK.StObj.Engine.Tests.Poco.PocoGenericTests;

namespace CK.StObj.Engine.Tests.CrisLike
{
    /// <summary>
    /// Marker interface to define mixable command parts.
    /// </summary>
    /// <remarks>
    /// Parts can be composed: when defining a specialized part that extends an
    /// existing <see cref="ICommandPart"/>, the <see cref="CKTypeDefinerAttribute"/> must be
    /// applied to the specialized part.
    /// </remarks>
    [CKTypeSuperDefiner]
    public interface ICommandPart : ICommand
    {
    }

}
