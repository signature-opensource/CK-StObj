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

        public FieldDefaultValue( object value, StringCodeWriter sharedCodeWriter )
            : this( value, WriteSourceValue( value, sharedCodeWriter ) )
        {
        }

        static string WriteSourceValue( object value, StringCodeWriter w )
        {
            if( w.StringBuilder.Length != 0 ) w = new StringCodeWriter();
            var source = w.Append( value ).ToString();
            w.StringBuilder.Clear();
            return source;
        }

        public static FieldDefaultValue? CreateFromParameter( IActivityMonitor monitor,
                                                              StringCodeWriter codeWriter,
                                                              ParameterInfo definer )
        {
            if( !definer.HasDefaultValue || definer.DefaultValue == null ) return null;
            return new FieldDefaultValue( definer.DefaultValue, codeWriter );
        }

        public static FieldDefaultValue? CreateFromAttribute( IActivityMonitor monitor,
                                                              StringCodeWriter sharedCodeWriter,
                                                              MemberInfo definer )
        {
            // Use the conversion from the constructor for the value.
            var a = definer.GetCustomAttribute<DefaultValueAttribute>();
            if( a == null ) return null;
            var value = a.Value;
            if( value == null ) return null;
            if( ReferenceEquals( value, String.Empty ) ) return StringDefault; 
            return new FieldDefaultValue( value, WriteSourceValue( value, sharedCodeWriter ) );
        }

        public bool CheckSameOrNone( IActivityMonitor monitor, MemberInfo defaultValueSource, StringCodeWriter codeWriter, MemberInfo other )
        {
            var a = other.GetCustomAttribute<DefaultValueAttribute>();
            if( a?.Value == null || a.Value == Value ) return true;
            var source = WriteSourceValue( a.Value, codeWriter );
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
