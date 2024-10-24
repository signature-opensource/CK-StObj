using CK.Core;
using System;
using System.Xml.Linq;

namespace CK.Setup;

/// <summary>
/// Models the &lt;Type&gt; elements that are children of <see cref="ExternalTypeConfigurationSet"/> &lt;Types&gt;.
/// </summary>
public sealed class ExternalTypeConfiguration
{
    readonly Type _type;
    readonly ConfigurableAutoServiceKind _kind;

    /// <summary>
    /// Gets the external type to configure.
    /// </summary>
    public Type Type => _type;

    /// <summary>
    /// Gets the kind.
    /// </summary>
    public ConfigurableAutoServiceKind Kind => _kind;

    /// <summary>
    /// Initializes a new <see cref="ExternalTypeConfiguration"/>.
    /// </summary>
    /// <param name="Type">The type</param>
    /// <param name="Kind">The type kind. Must not be <see cref="ConfigurableAutoServiceKind.None"/>.</param>
    public ExternalTypeConfiguration( Type type, ConfigurableAutoServiceKind kind )
    {
        Throw.CheckNotNullArgument( type );
        Throw.CheckArgument( kind is not ConfigurableAutoServiceKind.None );
        _type = type;
        _kind = kind;
    }

    internal ExternalTypeConfiguration( XElement e )
        : this( ReadType( e ), ReadKind( e ) )
    {
    }

    static Type ReadType( XElement e )
    {
        return SimpleTypeFinder.WeakResolver( (string?)e.Attribute( EngineConfiguration.xName ) ?? e.Value, throwOnError: true )!;
    }

    static ConfigurableAutoServiceKind ReadKind( XElement e )
    {
        var sK = e.AttributeRequired( EngineConfiguration.xKind ).Value;
        return Enum.Parse<ConfigurableAutoServiceKind>( sK.Replace( '|', ',' ) );
    }
}
