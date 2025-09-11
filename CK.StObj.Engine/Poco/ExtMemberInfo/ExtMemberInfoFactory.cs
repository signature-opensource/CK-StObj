using CK.Core;
using System;
using System.Reflection;

namespace CK.Setup;

/// <summary>
/// Implements <see cref="IExtMemberInfoFactory"/>.
/// </summary>
public sealed class ExtMemberInfoFactory : IExtMemberInfoFactory
{
    readonly NullabilityInfoContext _nullabilityContext;

    /// <summary>
    /// Initializes a new factory.
    /// </summary>
    public ExtMemberInfoFactory()
    {
        _nullabilityContext = new NullabilityInfoContext();
    }

    /// <inheritdoc />
    public IExtTypeInfo CreateNullOblivious( Type type )
    {
        Throw.CheckNotNullArgument( type );
        return new ExtTypeInfo( this, type );
    }

    /// <inheritdoc />
    public IExtMemberInfo Create( MemberInfo memberInfo )
    {
        Throw.CheckNotNullArgument( memberInfo );
        return memberInfo switch
        {
            PropertyInfo p => new ExtPropertyInfo( this, p ),
            FieldInfo p => new ExtFieldInfo( this, p ),
            EventInfo e => new ExtEventInfo( this, e ),
            Type t => new ExtTypeInfo( this, t ),
            _ => Throw.ArgumentException<IExtMemberInfo>( nameof( memberInfo ) )
        }; ;
    }


    /// <inheritdoc />
    public IExtParameterInfo Create( ParameterInfo parameterInfo )
    {
        Throw.CheckNotNullArgument( parameterInfo );
        return new ExtParameterInfo( this, parameterInfo );
    }

    /// <inheritdoc />
    public IExtPropertyInfo Create( PropertyInfo propertyInfo )
    {
        Throw.CheckNotNullArgument( propertyInfo );
        return new ExtPropertyInfo( this, propertyInfo );
    }

    /// <inheritdoc />
    public IExtFieldInfo Create( FieldInfo fieldInfo )
    {
        Throw.CheckNotNullArgument( fieldInfo );
        return new ExtFieldInfo( this, fieldInfo );
    }

    /// <inheritdoc />
    public IExtPropertyInfo CreateFake( PropertyInfo p,
                                        IExtNullabilityInfo homogeneousInfo,
                                        object[]? customAttributes,
                                        CustomAttributeData[]? customAttributesData )
    {
        Throw.CheckNotNullArgument( p );
        Throw.CheckArgument( homogeneousInfo?.IsHomogeneous == true );
        return new ExtPropertyInfo( this, p, homogeneousInfo, customAttributes, customAttributesData );
    }

    /// <inheritdoc />
    public IExtNullabilityInfo CreateNullabilityInfo( Type t )
    {
        Throw.CheckNotNullArgument( t );
        return new ExtNullabilityInfo( t );
    }

    /// <inheritdoc />
    public IExtEventInfo Create( EventInfo eventInfo )
    {
        Throw.CheckNotNullArgument( eventInfo );
        return new ExtEventInfo( this, eventInfo );
    }

    /// <inheritdoc />
    public IExtNullabilityInfo CreateNullabilityInfo( ParameterInfo parameterInfo, bool useReadState = true )
    {
        var i = _nullabilityContext.Create( parameterInfo );
        return new ExtNullabilityInfo( i, useReadState, false );
    }

    /// <inheritdoc />
    public IExtNullabilityInfo CreateNullabilityInfo( PropertyInfo propertyInfo, bool useReadState = true )
    {
        var i = _nullabilityContext.Create( propertyInfo );
        // For ref properties, the write state is incorrect (at least for Net7 implementation).
        // This fix the problem: ref properties always uses the read state.
        bool singleState;
        if( propertyInfo.PropertyType.IsByRef )
        {
            useReadState = true;
            singleState = true;
        }
        else
        {
            singleState = useReadState ? !propertyInfo.CanWrite : !propertyInfo.CanRead;
        }
        return new ExtNullabilityInfo( i, useReadState, singleState );
    }

    /// <inheritdoc />
    public IExtNullabilityInfo CreateNullabilityInfo( FieldInfo fieldInfo, bool useReadState = true )
    {
        var i = _nullabilityContext.Create( fieldInfo );
        return new ExtNullabilityInfo( i, useReadState, false );
    }

    /// <inheritdoc />
    public IExtNullabilityInfo CreateNullabilityInfo( EventInfo eventInfo )
    {
        var i = _nullabilityContext.Create( eventInfo );
        return new ExtNullabilityInfo( i, true, true );
    }
}
