using CK.Core;
using System;

namespace CK.CrisLike
{
    /// <summary>
    /// The base command interface marker is a simple <see cref="IPoco"/>.
    /// Any type that extends this interface defines a new command type.
    /// </summary>
    [CKTypeDefiner]
    public interface ICommand : IPoco
    {
        // This is disabled since we do not generate the C# code that implements the
        // CommandModel. Removing this enables a valid C# code.

        ///// <summary>
        ///// Gets the<see cref= "ICommandModel" /> that describes this command.
        ///// This property is automatically implemented. 
        ///// </summary>
        /////
        //[AutoImplementationClaim]
        //ICommandModel CommandModel { get; }
    }
}
