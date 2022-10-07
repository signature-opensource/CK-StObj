using CK.CodeGen;
using CK.Core;
using CK.Core.Impl;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace CK.Setup
{
    partial class PocoRegistrar
    {
        sealed class DefaultValue : IPocoPropertyDefaultValue
        {
            DefaultValue( IPocoPropertyImpl definer, Type? type, object? value, string source )
            {
                Definer = definer;
                DefaultValueType = type;
                Value = value;
                ValueCSharpSource = source;
            }

            public static bool TryCreate( IActivityMonitor monitor, IPocoPropertyImpl definer, out DefaultValue? defaultValue )
            {
                defaultValue = null;
                // Gets the target converted type if the constructor has 2 arguments.
                var aData = definer.Info.GetCustomAttributesData().FirstOrDefault( x => x.AttributeType == typeof( DefaultValueAttribute ) );
                if( aData == null ) return true;
                var cArgs = aData.ConstructorArguments;
                Type? convertType = cArgs.Count == 2 ? (Type?)cArgs[0].Value : null;
                // Use the conversion from the constructor for the value.
                var a = definer.Info.GetCustomAttribute<DefaultValueAttribute>();
                if( a == null )
                {
                    monitor.Error( $"{definer} has DefaultValueAttribute custom data but no attribute. This is not possible!" );
                    return false;
                }
                var value = a.Value;
                // CSharp code is computed from the value.
                var w = new StringCodeWriter();
                var source = w.Append( value ).ToString();
                defaultValue = new DefaultValue( definer, convertType, value, source );
                return true;
            }

            public bool CheckSameOrNone( IActivityMonitor monitor, IPocoPropertyImpl p )
            {
                var a = p.Info.GetCustomAttribute<DefaultValueAttribute>();
                if( a == null || a.Value == Value ) return true;
                var w = new StringCodeWriter();
                var source = w.Append( a.Value ).ToString();
                monitor.Error( $"Default values difference between {Definer} = {ValueCSharpSource} and {p} = {source}." );
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

            /// <summary>
            /// Gets the first property that defines this default value.
            /// Other properties must define the same default value.
            /// </summary>
            public IPocoPropertyImpl Definer { get; }
        }
    }
}
