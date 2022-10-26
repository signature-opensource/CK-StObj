using CK.CodeGen;
using CK.Core;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace CK.Setup
{
    sealed class FieldDefaultValue : IPocoFieldDefaultValue
    {
        public static readonly FieldDefaultValue Invalid = new FieldDefaultValue( String.Empty, String.Empty );
        public static readonly FieldDefaultValue StringDefault = new FieldDefaultValue( String.Empty, "\"\"" );
        public static readonly FieldDefaultValue DateTimeDefault = new FieldDefaultValue( Util.UtcMinValue, "CK.Core.Util.UtcMinValue" );

        public FieldDefaultValue( object value, string source )
        {
            Value = value;
            ValueCSharpSource = source;
        }

        public FieldDefaultValue( object value, PocoTypeSystem.IStringBuilderPool sbPool )
            : this( value, WriteSourceValue( value, sbPool ) )
        {
        }

        static string WriteSourceValue( object value, PocoTypeSystem.IStringBuilderPool sbPool )
        {
            var w = new StringCodeWriter( sbPool.Get() );
            var source = w.Append( value ).ToString();
            sbPool.GetStringAndReturn( w.StringBuilder );
            return source;
        }

        public static FieldDefaultValue? CreateFromParameter( IActivityMonitor monitor,
                                                              PocoTypeSystem.IStringBuilderPool sbPool,
                                                              ParameterInfo definer )
        {
            if( !definer.HasDefaultValue || definer.DefaultValue == null ) return null;
            return new FieldDefaultValue( definer.DefaultValue, sbPool );
        }

        public static FieldDefaultValue? CreateFromAttribute( IActivityMonitor monitor,
                                                              PocoTypeSystem.IStringBuilderPool sbPool,
                                                              MemberInfo definer )
        {
            // Use the conversion from the constructor for the value.
            var a = definer.GetCustomAttribute<DefaultValueAttribute>();
            if( a == null ) return null;
            var value = a.Value;
            if( value == null ) return null;
            if( ReferenceEquals( value, String.Empty ) ) return StringDefault; 
            return new FieldDefaultValue( value, WriteSourceValue( value, sbPool ) );
        }

        public bool CheckSameOrNone( IActivityMonitor monitor, MemberInfo defaultValueSource, PocoTypeSystem.IStringBuilderPool sbPool, MemberInfo other )
        {
            var a = other.GetCustomAttribute<DefaultValueAttribute>();
            if( a?.Value == null || a.Value == Value ) return true;
            var source = WriteSourceValue( a.Value, sbPool );
            if( source != ValueCSharpSource )
            {
                monitor.Error( $"Default values difference between '{defaultValueSource.DeclaringType}.{defaultValueSource.Name}' = '{ValueCSharpSource}' and '{other.DeclaringType}.{other.Name}' = '{source}'." );
                return false;
            }
            return true;
        }

        /// <summary>
        /// Gets the default value.
        /// </summary>
        public object Value { get; }

        /// <summary>
        /// Gets the default value in C# source code.
        /// </summary>
        public string ValueCSharpSource { get; }

        public override string ToString() => ValueCSharpSource;
    }
}