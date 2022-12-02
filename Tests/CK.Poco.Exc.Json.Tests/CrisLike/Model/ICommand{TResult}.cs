using CK.Core;
using System;

namespace CK.CrisLike
{
    /// <summary>
    /// Describes a type of command that expects a result.
    /// </summary>
    /// <typeparam name="TResult">Type of the expected result.</typeparam>
    [CKTypeDefiner]
    public interface ICommand<out TResult> : ICommand
    {
    }


}
