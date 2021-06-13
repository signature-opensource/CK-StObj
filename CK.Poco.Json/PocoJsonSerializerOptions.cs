using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace CK.Core
{
    /// <summary>
    /// Describes dynamic serialization options.
    /// </summary>
    public class PocoJsonSerializerOptions
    {
        /// <summary>
        /// Initializes a new default options.
        /// </summary>
        public PocoJsonSerializerOptions()
        {
            Mode = PocoSerializerMode.Server;
        }

        /// <summary>
        /// Gets or sets the serialization mode.
        /// Defaults to <see cref="PocoSerializerMode.Server"/>.
        /// </summary>
        public PocoSerializerMode Mode { get; set; }

    }
}
