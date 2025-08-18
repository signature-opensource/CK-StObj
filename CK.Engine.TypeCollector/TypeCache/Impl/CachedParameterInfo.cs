using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;

namespace CK.Engine.TypeCollector;

public sealed class CachedParameterInfo
{
    readonly CachedMethodBase _method;
    readonly ParameterInfo _parameterInfo;
    ImmutableArray<CustomAttributeData> _attributesData;
    ImmutableArray<ICachedType> _attributesDataType;
    ICachedType? _parameterType;
    string? _toString;

    internal CachedParameterInfo( CachedMethodBase method, ParameterInfo parameterInfo )
    {
        _method = method;
        _parameterInfo = parameterInfo;
    }

    /// <summary>
    /// Gets the method that contains this parameter.
    /// </summary>
    public ICachedMethodBase Method => _method;

    /// <summary>
    /// Gets the parameter name.
    /// <para>
    /// This is null if this parameter is the <see cref="CachedMethodInfo.ReturnParameter"/>.
    /// </para>
    /// </summary>
    public string? Name => _parameterInfo.Name;

    /// <summary>
    /// Gets whether this is the returned parameter (its <see cref="Name"/> is null).
    /// </summary>
    [MemberNotNullWhen( true, nameof( Name ) )]
    public bool IsReturnedParameter => _parameterInfo.Name == null;

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
    public ImmutableArray<CustomAttributeData> AttributesData
    {
        get
        {
            if( _attributesData.IsDefault )
            {
                // Canot use ImmutableCollectionsMarshal.AsImmutableArray here: CustomAttributeData
                // can be retrieved by IList<CustomAttributeData> or IEnumerable<CustomAttributeData> only.
                _attributesData = _parameterInfo.CustomAttributes.ToImmutableArray();
            }
            return _attributesData;
        }
    }

    /// <summary>
    /// Gets the parameter attribute data types.
    /// </summary>
    public ImmutableArray<ICachedType> AttributesDataType
    {
        get
        {
            if( _attributesDataType.IsDefault )
            {
                var b = ImmutableArray.CreateBuilder<ICachedType>( AttributesData.Length );
                foreach( CustomAttributeData a in _attributesData )
                {
                    b.Add( _method.TypeCache.Get( a.AttributeType ) );
                }
                _attributesDataType = b.MoveToImmutable();
            }
            return _attributesDataType;
        }
    }

    /// <summary>
    /// Writes this parameter with its <see cref="AttributesDataType"/> names, its <see cref="ParameterType"/>
    /// name (without namespace) and parameter name if this is not a returned parameter.
    /// </summary>
    /// <param name="b"></param>
    /// <returns></returns>
    public StringBuilder Write( StringBuilder b )
    {
        if( AttributesData.Length > 0 )
        {
            b.Append( '[' );
            for( int i = 0; i < AttributesDataType.Length; i++ )
            {
                if( i != 0 ) b.Append( ", " );
                b.Append( AttributesDataType[i].Name );
            }
            b.Append( ']' );
        }
        b.Append( ParameterType.Name );
        var n = Name;
        if( n != null )
        {
            b.Append( ' ' ).Append( n );
        }
        return b;
    }

    public override string ToString() => _toString ??= Write( new StringBuilder() ).ToString();
}
