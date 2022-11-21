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
            return new ExtMemberInfo( this, parameterInfo );
        }

        public IExtPropertyInfo Create( PropertyInfo propertyInfo )
        {
            Throw.CheckNotNullArgument( propertyInfo );
            return new ExtMemberInfo( this, propertyInfo );
        }

        public IExtFieldInfo Create( FieldInfo fieldInfo )
        {
            Throw.CheckNotNullArgument( fieldInfo );
            return new ExtMemberInfo( this, fieldInfo );
        }

        public IExtEventInfo Create( EventInfo eventInfo )
        {
            Throw.CheckNotNullArgument( eventInfo );
            return new ExtMemberInfo( this, eventInfo );
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

        internal IExtNullabilityInfo CreateNullabilityInfo( ExtMemberInfo m, bool useReadState = true )
        {
            return m.AsPropertyInfo != null
                    ? CreateNullabilityInfo( m.AsPropertyInfo.PropertyInfo, useReadState )
                    : m.AsFieldInfo != null
                    ? CreateNullabilityInfo( m.AsFieldInfo.FieldInfo, useReadState )
                    : m.AsEventInfo != null
                    ? CreateNullabilityInfo( m.AsEventInfo.EventInfo )
                    : m.AsParameterInfo != null
                    ? CreateNullabilityInfo( m.AsParameterInfo.ParameterInfo, useReadState )
                    : Throw.NotSupportedException<IExtNullabilityInfo>();
        }
    }
}
