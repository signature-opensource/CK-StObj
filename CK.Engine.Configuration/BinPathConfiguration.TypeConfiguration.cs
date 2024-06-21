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
        /// <param name="Type">The type</param>
        /// <param name="Kind">The type kind. <see cref="AutoServiceKind.None"/> only ensures that the type is registered.</param>
        public sealed record TypeConfiguration( Type Type, AutoServiceKind Kind = AutoServiceKind.None )
        {
            /// <summary>
            /// Initializes a new <see cref="TypeConfiguration"/> from a Xml element.
            /// </summary>
            /// <param name="e">The Xml element.</param>
            public TypeConfiguration( XElement e )
                : this( ReadType( e ), ReadKind( e ) )
            {
                Type = SimpleTypeFinder.WeakResolver( (string?)e.Attribute( EngineConfiguration.xName ) ?? e.Value, throwOnError: true )!;
                ReadKind( e );
            }

            static Type ReadType( XElement e )
            {
                return SimpleTypeFinder.WeakResolver( (string?)e.Attribute( EngineConfiguration.xName ) ?? e.Value, throwOnError: true )!;
            }

            static AutoServiceKind ReadKind( XElement e )
            {
                var k = (string?)e.Attribute( EngineConfiguration.xKind );
                return k != null ? (AutoServiceKind)Enum.Parse( typeof( AutoServiceKind ), k.Replace( '|', ',' ) ) : AutoServiceKind.None;
            }
        }
    }

}
