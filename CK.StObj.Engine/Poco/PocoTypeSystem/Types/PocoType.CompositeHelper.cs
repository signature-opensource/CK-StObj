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
            /// then no code is required (<see cref="DefaultValueInfo.DefaultValue"/> is null).
            /// </summary>
            public static DefaultValueInfo CreateDefaultValueInfo( IActivityMonitor monitor,
                                                                   PocoTypeSystem.IStringBuilderPool sbPool,
                                                                   IRecordPocoType type )
            {
                var b = sbPool.Get();
                var r = type.IsAnonymous
                            ? ForAnonymousRecord( monitor, b, type )
                            : ForRecord( monitor, b, type );
                sbPool.Return( b );
                return r;

                static DefaultValueInfo ForAnonymousRecord( IActivityMonitor monitor, StringBuilder b, IRecordPocoType type )
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
                        }
                        else
                        {
                            b.Append( "default" );
                        }
                    }
                    b.Append( ')' );
                    return requiresInit
                            ? new DefaultValueInfo( new FieldDefaultValue( b.ToString() ) )
                            : DefaultValueInfo.Allowed;
                }

                static DefaultValueInfo ForRecord( IActivityMonitor monitor,
                                                   StringBuilder b,
                                                   IRecordPocoType type )
                {
                    bool atLeasOne = false;
                    foreach( var f in type.Fields )
                    {
                        var fInfo = f.DefaultValueInfo;
                        if( fInfo.IsAllowed ) continue;
                        if( fInfo.IsDisallowed ) return OnDisallowed( monitor, type, f );
                        Debug.Assert( fInfo.RequiresInit );
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
                        return new DefaultValueInfo( new FieldDefaultValue( b.ToString() ) );
                    }
                    return DefaultValueInfo.Allowed;
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
                                                                   PocoTypeSystem.IStringBuilderPool sbPool,
                                                                   IPrimaryPocoType type )
            {
                var b = sbPool.Get();
                var r = DoCreateDefaultValueInfo( monitor, b, type );
                sbPool.Return( b );
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
                            monitor.Error( $"Unable to obtain a default value for '{f.Name}', on '{type.CSharpName}' default value cannot be generated. Should this be nullable?" );
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
                        var text = b.ToString();
                        return new DefaultValueInfo( new FieldDefaultValue( text ) );
                    }
                    return DefaultValueInfo.Allowed;
                }

            }

        }

    }
}
