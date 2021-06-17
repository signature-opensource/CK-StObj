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
        /// Gets or sets the serialization mode.
        /// Defaults to <see cref="PocoJsonSerializerMode.ECMAScriptSafe"/>.
        /// </summary>
        public PocoJsonSerializerMode Mode { get; set; }

    }
}
