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

        public ExtMemberInfoFactory()
        {
            _nullabilityContext = new TEMPNullabilityInfoContext();
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
