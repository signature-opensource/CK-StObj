using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Text;

namespace CK.Engine.TypeCollector;

public sealed class CachedParameterInfo
{
    readonly CachedMethodInfo _method;
    readonly ParameterInfo _parameterInfo;
    ImmutableArray<CustomAttributeData> _customAttributes;
    ICachedType? _parameterType;

    internal CachedParameterInfo( CachedMethodInfo method, ParameterInfo parameterInfo )
    {
        _method = method;
        _parameterInfo = parameterInfo;
    }

    /// <summary>
    /// Gets the method that contains this parameter.
    /// </summary>
    public CachedMethodInfo MethodInfo => _method;

    /// <summary>
    /// Gets the parameter name.
    /// <para>
    /// This is null if this parameter is the <see cref="CachedMethodInfo.ReturnParameter"/>.
    /// </para>
    /// </summary>
    public string? Name => _parameterInfo.Name;

    /// <summary>
    /// Gets the cached info. Should rarely be used directly.
    /// </summary>
    public ParameterInfo ParameterInfo => _parameterInfo;

    /// <summary>
    /// Gets the parameter type.
    /// </summary>
    public ICachedType ParameterType => _parameterType ??= _method.DeclaringType.TypeCache.Get( _parameterInfo.ParameterType );

    /// <summary>
    /// Gets the parameter attributes data.
    /// </summary>
    public ImmutableArray<CustomAttributeData> CustomAttributes
    {
        get
        {
            if( _customAttributes.IsDefault )
            {
                // Canot use ImmutableCollectionsMarshal.AsImmutableArray here: CustomAttributeData
                // can be retrieved by IList<CustomAttributeData> or IEnumerable<CustomAttributeData> only.
                _customAttributes = _parameterInfo.CustomAttributes.ToImmutableArray();
            }
            return _customAttributes;
        }
    }

    public StringBuilder Write( StringBuilder b )
    {
        b.Append( ParameterType.CSharpName );
        var n = Name;
        if( n != null )
        {
            b.Append( ' ' ).Append( n );
        }
        return b;
    }

    public override string ToString() => Write( new StringBuilder() ).ToString();
}
