using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.StObj.Engine.Tests.CrisLike
{
    /// <summary>
    /// Command directory that contains all the available command in the context.
    /// </summary>
    [CK.Setup.ContextBoundDelegation( "CK.StObj.Engine.Tests.CrisLike.CrisCommandDirectoryLikeImpl, CK.StObj.Engine.Tests" )]
    public abstract class CrisCommandDirectoryLike : ISingletonAutoService
    {
        protected CrisCommandDirectoryLike( IReadOnlyList<ICommandModel> commands )
        {
            Commands = commands;
        }

        /// <summary>
        /// Gets all the commands indexed by their <see cref="ICommandModel.CommandIdx"/>.
        /// </summary>
        public IReadOnlyList<ICommandModel> Commands { get; }

    }
}
