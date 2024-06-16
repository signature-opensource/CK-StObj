using CK.Core;
using System;
using System.Xml.Linq;

namespace CK.Setup
{
    public sealed partial class BinPathConfiguration
    {
        /// <summary>
        /// Models the &lt;Type&gt; elements that are children of &lt;Types&gt;.
        /// </summary>
        public sealed class TypeConfiguration
        {
            /// <summary>
            /// Initializes a new <see cref="TypeConfiguration"/> from a Xml element.
            /// </summary>
            /// <param name="e">The Xml element.</param>
            public TypeConfiguration( XElement e )
            {
                Name = (string?)e.Attribute( EngineConfiguration.xName ) ?? e.Value;
                var k = (string?)e.Attribute( EngineConfiguration.xKind );
                if( k != null ) Kind = (AutoServiceKind)Enum.Parse( typeof( AutoServiceKind ), k.Replace( '|', ',' ) );
                Optional = (bool?)e.Attribute( EngineConfiguration.xOptional ) ?? false;
            }

            /// <summary>
            /// Initializes a new <see cref="TypeConfiguration"/>.
            /// </summary>
            /// <param name="name">Assembly qualified name of the type.</param>
            /// <param name="kind">The service kind.</param>
            /// <param name="optional">Whether the type may not exist.</param>
            public TypeConfiguration( string name, AutoServiceKind kind, bool optional )
            {
                Name = name;
                Kind = kind;
                Optional = optional;
            }

            /// <summary>
            /// Gets or sets the assembly qualified name of the type.
            /// This should not be null or whitespace, nor appear more than once in the <see cref="Types"/> collection otherwise
            /// this configuration is considered invalid.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Gets or sets the service kind. Defaults to <see cref="AutoServiceKind.None"/>.
            /// Note that this None value may be used along with a false <see cref="Optional"/> to check the existence
            /// of a type.
            /// </summary>
            public AutoServiceKind Kind { get; set; }

            /// <summary>
            /// Gets or sets whether this type is optional: if the <see cref="Name"/> cannot be resolved
            /// a warning is emitted.
            /// Defaults to false: by default, if the type is not found at runtime, an error is raised.
            /// </summary>
            public bool Optional { get; set; }

            /// <summary>
            /// Overridden to return the Name - Kind and Optional value.
            /// This is used as the equality key when configurations are grouped into similar bin paths.
            /// </summary>
            /// <returns>A readable string.</returns>
            public override string ToString() => $"{Name} - {Kind} - {Optional}";
        }
    }

}
