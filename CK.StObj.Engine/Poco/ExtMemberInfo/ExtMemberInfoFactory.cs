using CK.CodeGen;
using CK.Core;
using System;
using System.Reflection;
using System.Reflection.Metadata;
using System.Threading;

namespace CK.Setup
{
    public sealed class ExtMemberInfoFactory : IExtMemberInfoFactory
    {
        readonly TEMPNullabilityInfoContext _nullabilityContext;

        static MethodInfo? _typeBuilder;
        static T TypeBuilder<T>() => default!;

        public ExtMemberInfoFactory()
        {
            _nullabilityContext = new TEMPNullabilityInfoContext();
        }

        public IExtTypeInfo Create( Type type )
        {
            Throw.CheckNotNullArgument( type );
            return new ExtTypeInfo( this, type );
        }

        public IExtParameterInfo Create( ParameterInfo parameterInfo )
        {
            Throw.CheckNotNullArgument( parameterInfo );
            return new ExtParameterInfo( this, parameterInfo );
        }

        public IExtPropertyInfo Create( PropertyInfo propertyInfo )
        {
            Throw.CheckNotNullArgument( propertyInfo );
            return new ExtPropertyInfo( this, propertyInfo );
        }

        public IExtFieldInfo Create( FieldInfo fieldInfo )
        {
            Throw.CheckNotNullArgument( fieldInfo );
            return new ExtFieldInfo( this, fieldInfo );
        }

        public IExtPropertyInfo CreateFake( PropertyInfo p,
                                            IExtNullabilityInfo homogeneousInfo,
                                            object[]? customAttributes,
                                            CustomAttributeData[]? customAttributesData )
        {
            Throw.CheckNotNullArgument( p );
            Throw.CheckArgument( homogeneousInfo?.IsHomogeneous == true );
            return new ExtPropertyInfo( this, p, homogeneousInfo, customAttributes, customAttributesData );
        }

        public IExtEventInfo Create( EventInfo eventInfo )
        {
            Throw.CheckNotNullArgument( eventInfo );
            return new ExtEventInfo( this, eventInfo );
        }

        public IExtNullabilityInfo CreateNullabilityInfo( Type t, bool toNullable = false )
        {
            // Okay... This is not the greatest code ever written, but it does its job...
            ParameterInfo p;
            _typeBuilder ??= typeof( ExtMemberInfoFactory ).GetMethod( nameof( TypeBuilder ), BindingFlags.Static | BindingFlags.NonPublic )!;
            p = (_typeBuilder.MakeGenericMethod( t )).ReturnParameter;
            var r = CreateNullabilityInfo( p, true );
            return toNullable ? r.ToNullable() : r;
        }

        public IExtNullabilityInfo CreateNullabilityInfo( ParameterInfo parameterInfo, bool useReadState = true )
        {
            var i = _nullabilityContext.Create( parameterInfo );
            return new ExtNullabilityInfo( i, useReadState, false );
        }

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

        public IExtNullabilityInfo CreateNullabilityInfo( FieldInfo fieldInfo, bool useReadState = true )
        {
            var i = _nullabilityContext.Create( fieldInfo );
            return new ExtNullabilityInfo( i, useReadState, false );
        }

        public IExtNullabilityInfo CreateNullabilityInfo( EventInfo eventInfo )
        {
            var i = _nullabilityContext.Create( eventInfo );
            return new ExtNullabilityInfo( i, true, true );
        }
    }
}
