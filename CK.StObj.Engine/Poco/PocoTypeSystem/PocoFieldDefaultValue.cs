using CK.CodeGen;
using CK.Core;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace CK.Setup
{
    sealed class PocoFieldDefaultValue : IPocoFieldDefaultValue
    {
        public static readonly PocoFieldDefaultValue StringDefault = new PocoFieldDefaultValue( null, String.Empty, "\"\"" );

        PocoFieldDefaultValue( Type? type, object? value, string source )
        {
            DefaultValueType = type;
            Value = value;
            ValueCSharpSource = source;
        }

        PocoFieldDefaultValue( Type? type, object? value, StringCodeWriter codeWriter )
            : this( type, value, WriteSourceValue( value, codeWriter ) )
        {
        }

        static string WriteSourceValue( object? value, StringCodeWriter codeWriter )
        {
            Debug.Assert( codeWriter.StringBuilder.Length == 0 );
            var source = codeWriter.Append( value ).ToString();
            codeWriter.StringBuilder.Clear();
            return source;
        }

        public static bool TryCreate( IActivityMonitor monitor, StringCodeWriter codeWriter, ParameterInfo definer, out PocoFieldDefaultValue? defaultValue )
        {
            defaultValue = null;
            if( !definer.HasDefaultValue ) return true;
            defaultValue = new PocoFieldDefaultValue( null, definer.DefaultValue, codeWriter );
            return true;
        }

        public static bool TryCreate( IActivityMonitor monitor, StringCodeWriter codeWriter, MemberInfo definer, out PocoFieldDefaultValue? defaultValue )
        {
            defaultValue = null;
            // Gets the target converted type if the constructor has 2 arguments.
            var aData = definer.GetCustomAttributesData().FirstOrDefault( x => x.AttributeType == typeof( DefaultValueAttribute ) );
            if( aData == null ) return true;
            var cArgs = aData.ConstructorArguments;
            Type? convertType = cArgs.Count == 2 ? (Type?)cArgs[0].Value : null;
            // Use the conversion from the constructor for the value.
            var a = definer.GetCustomAttribute<DefaultValueAttribute>();
            if( a == null )
            {
                monitor.Error( $"'{definer.DeclaringType.ToCSharpName()}.{definer.Name}' has DefaultValueAttribute custom data but no attribute. This is not possible!" );
                return false;
            }
            var value = a.Value;
            // CSharp code is computed from the value.
            defaultValue = new PocoFieldDefaultValue( convertType, value, codeWriter );
            return true;
        }

        public bool CheckSameOrNone( IActivityMonitor monitor, StringCodeWriter codeWriter, MemberInfo other )
        {
            var a = other.GetCustomAttribute<DefaultValueAttribute>();
            if( a == null || a.Value == Value ) return true;
            Debug.Assert( codeWriter.StringBuilder.Length == 0 );
            var source = codeWriter.Append( a.Value ).ToString();
            codeWriter.StringBuilder.Clear();
            monitor.Error( $"Default values difference between {ValueCSharpSource} and {other} = {source}." );
            return false;
        }

        /// <summary>
        /// Gets the default type value (for [<see cref="DefaultValueAttribute(Type,string)"/>]).
        /// </summary>
        public Type? DefaultValueType { get; }

        /// <summary>
        /// Gets the default value.
        /// </summary>
        public object? Value { get; }

        /// <summary>
        /// Gets the default value in C# source code.
        /// </summary>
        public string ValueCSharpSource { get; }
    }
}
