using CK.CodeGen;
using CK.Core;
using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Linq;

namespace CK.Setup
{

    partial class PocoType
    {
        /// <summary>
        /// The comma string must be the same across any default values.
        /// </summary>
        public const string Comma = ", ";

        static class CompositeHelper
        {
            /// <summary>
            /// Generates the initialization list syntax of a record.
            /// The code is in <see cref="IPocoFieldDefaultValue.ValueCSharpSource"/>.
            /// If the resulting DefaultValueInfo.IsDisabled, then an error occurred, if DefaultValueInfo.IsAllowed
            /// the no code is required (<see cref="DefaultValueInfo.DefaultValue"/> is null).
            /// </summary>
            public static DefaultValueInfo CreateDefaultValueInfo( IActivityMonitor monitor,
                                                                   StringBuilder sharedBuilder,
                                                                   IRecordPocoType type )
            {
                if( !TryInstantiateValue( monitor, type, out var oValue ) )
                {
                    return DefaultValueInfo.Disallowed;
                }
                var b = sharedBuilder.Length == 0 ? sharedBuilder : new StringBuilder();
                var r = type.IsAnonymous
                            ? ForAnonymousRecord( monitor, b, type, oValue )
                            : ForRecord( monitor, b, type, oValue );
                b.Clear();
                return r;

                static bool TryInstantiateValue( IActivityMonitor monitor, ICompositePocoType type, out object oValue )
                {
                    oValue = null!;
                    Exception? error = null;
                    try
                    {
                        oValue = Activator.CreateInstance( type.Type )!;
                        if( oValue != null ) return true;
                    }
                    catch( Exception ex )
                    {
                        error = ex;
                    }
                    monitor.Fatal( $"Unable to instantiate a default value instance for record '{type.CSharpName}'.", error );
                    return false;
                }

                static DefaultValueInfo ForAnonymousRecord( IActivityMonitor monitor, StringBuilder b, IRecordPocoType type, object oValue )
                {
                    var fieldInfos = type.Type.GetFields();
                    // We always build the default value string because we need
                    // positional values with 'default'. If it's all 'default', we forget the string
                    // and returns Allowed. This may be not optimal if a lot of Allowed occur but this
                    // is simpler and there should not be a lot of pure default values.
                    bool requiresInit = false;
                    b.Append( '(' );
                    foreach( var f in type.Fields )
                    {
                        var fInfo = f.DefaultValueInfo;
                        if( fInfo.IsDisallowed ) return OnDisallowed( monitor, type, f );
                        if( f.Index > 0 ) b.Append( Comma );
                        if( fInfo.RequiresInit )
                        {
                            requiresInit = true;
                            b.Append( fInfo.DefaultValue.ValueCSharpSource );
                            fieldInfos[f.Index].SetValue( oValue, fInfo.DefaultValue.Value );
                        }
                        else
                        {
                            b.Append( "default" );
                        }
                    }
                    b.Append( ')' );
                    return requiresInit
                            ? new DefaultValueInfo( new FieldDefaultValue( oValue, b.ToString() ) )
                            : DefaultValueInfo.Allowed;
                }

                static DefaultValueInfo ForRecord( IActivityMonitor monitor,
                                                   StringBuilder b,
                                                   IRecordPocoType type,
                                                   object oValue )
                {
                    bool atLeasOne = false;
                    foreach( var f in type.Fields )
                    {
                        var fInfo = f.DefaultValueInfo;
                        if( fInfo.IsAllowed ) continue;
                        if( fInfo.IsDisallowed ) return OnDisallowed( monitor, type, f );
                        Debug.Assert( fInfo.RequiresInit );
                        // Configure the default instance.
                        if( !TrySetFieldOnDefaultValueInstance( monitor, type, oValue, f, fInfo.DefaultValue.Value ) )
                        {
                            return DefaultValueInfo.Disallowed;
                        }
                        // Generate the source code for the initialization.
                        if( atLeasOne )
                        {
                            b.Append( Comma );
                        }
                        else
                        {
                            b.Append( "new(){" );
                            atLeasOne = true;
                        }
                        b.Append( f.Name ).Append( " = " ).Append( fInfo.DefaultValue.ValueCSharpSource );
                    }
                    if( atLeasOne )
                    {
                        b.Append( '}' );
                        return new DefaultValueInfo( new FieldDefaultValue( oValue, b.ToString() ) );
                    }
                    return DefaultValueInfo.Allowed;

                    static bool TrySetFieldOnDefaultValueInstance( IActivityMonitor monitor,
                                                                   IRecordPocoType type,
                                                                   object oValue,
                                                                   IRecordPocoField field,
                                                                   object? value )
                    {
                        try
                        {
                            if( type.IsAnonymous )
                            {
                                var fields = type.Type.GetFields();
                            }
                            else
                            {
                                var p = type.Type.GetProperty( field.Name );
                                if( p != null )
                                {
                                    p.SetValue( oValue, value );
                                }
                                else
                                {
                                    var f = type.Type.GetField( field.Name );
                                    if( f != null )
                                    {
                                        f.SetValue( oValue, value );
                                    }
                                    else
                                    {
                                        monitor.Error( $"Unable to get a FieldInfo or PropertyInfo for field '{field}' in record '{type.CSharpName}' to configure default instance." );
                                        return false;
                                    }
                                }
                            }
                        }
                        catch( Exception ex )
                        {
                            monitor.Error( $"Unable to configure default instance for field '{field}' in record '{type.CSharpName}'.", ex );
                            return false;
                        }
                        return true;
                    }
                }


                static DefaultValueInfo OnDisallowed( IActivityMonitor monitor, IRecordPocoType type, IRecordPocoField f )
                {
                    monitor.Error( $"Unable to obtain a default value for record field '{type.CSharpName}.{f.Name}', default value cannot be generated." );
                    return DefaultValueInfo.Disallowed;
                }
            }

            /// <summary>
            /// Generates the constructor code of a Poco (regular statements).
            /// The code is in <see cref="IPocoFieldDefaultValue.ValueCSharpSource"/>.
            /// If the resulting DefaultValueInfo.IsDisabled, then an error occurred, if DefaultValueInfo.IsAllowed
            /// then no code is required (<see cref="DefaultValueInfo.DefaultValue"/> is null).
            /// </summary>
            public static DefaultValueInfo CreateDefaultValueInfo( IActivityMonitor monitor,
                                                                   StringBuilder sharedBuilder,
                                                                   IPrimaryPocoType type )
            {
                var b = sharedBuilder.Length == 0 ? sharedBuilder : new StringBuilder();
                var r = DoCreateDefaultValueInfo( monitor, b, type );
                b.Clear();
                return r;

                static DefaultValueInfo DoCreateDefaultValueInfo( IActivityMonitor monitor,
                                                                  StringBuilder b,
                                                                  IPrimaryPocoType type )
                {
                    bool atLeasOne = false;
                    foreach( var f in type.Fields )
                    {
                        var fInfo = f.DefaultValueInfo;
                        // If the field is Allowed, skip it.
                        if( fInfo.IsAllowed ) continue;
                        if( fInfo.IsDisallowed )
                        {
                            monitor.Error( $"Unable to obtain a default value for field '{f.Name}', record '{type.CSharpName}' default value cannot be generated." );
                            return DefaultValueInfo.Disallowed;
                        }
                        Debug.Assert( fInfo.RequiresInit );
                        // Generate the source code for the initialization.
                        if( atLeasOne )
                        {
                            b.Append( ';' ).Append( Environment.NewLine );
                        }
                        else
                        {
                            atLeasOne = true;
                        }
                        b.Append( f.PrivateFieldName ).Append( " = " ).Append( fInfo.DefaultValue.ValueCSharpSource );
                    }
                    // Success: if no field have been initialized, the default value is useless => Allowed.
                    if( atLeasOne )
                    {
                        b.Append( ';' );
                        // Use the text for the default value instance (unused when generating Poco constructor).
                        var text = b.ToString();
                        return new DefaultValueInfo( new FieldDefaultValue( text, text ) );
                    }
                    return DefaultValueInfo.Allowed;
                }

            }

        }

    }
}
