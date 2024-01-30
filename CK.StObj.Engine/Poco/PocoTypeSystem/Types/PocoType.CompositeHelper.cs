using CK.CodeGen;
using CK.Core;
using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Linq;
using static CK.CodeGen.TupleTypeName;

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
            /// If the result is DefaultValueInfo.RequiresInit, the initializer code is in <see cref="IPocoFieldDefaultValue.ValueCSharpSource"/>.
            /// If the result is DefaultValueInfo.IsAllowed then no code is required (<see cref="DefaultValueInfo.DefaultValue"/> is null).
            /// If the result is DefaultValueInfo.Disallowed, then... well, it's disabled: this cannot be used in a Poco field, only in collections
            /// and outside of a Poco.
            /// </summary>
            public static DefaultValueInfo CreateDefaultValueInfo( PocoTypeSystemBuilder.IStringBuilderPool sbPool, IRecordPocoType type )
            {
                var b = sbPool.Get();
                var r = type.IsAnonymous
                            ? ForAnonymousRecord( b, type )
                            : ForRecord( b, type );
                sbPool.Return( b );
                return r;

                static DefaultValueInfo ForAnonymousRecord( StringBuilder b, IRecordPocoType type )
                {
                    // We always build the default value string because we need
                    // positional values with 'default'. If it's all 'default', we forget the string
                    // and returns Allowed. This may be not optimal if a lot of Allowed occur but this
                    // is simpler and there should not be a lot of pure default values.
                    bool requiresInit = false;
                    b.Append( '(' );
                    foreach( var f in type.Fields )
                    {
                        var fInfo = f.DefaultValueInfo;
                        if( fInfo.IsDisallowed ) return DefaultValueInfo.Disallowed;
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

                static DefaultValueInfo ForRecord( StringBuilder b, IRecordPocoType type )
                {
                    bool atLeasOne = false;
                    foreach( var f in type.Fields )
                    {
                        var fInfo = f.DefaultValueInfo;
                        if( fInfo.IsAllowed ) continue;
                        if( fInfo.IsDisallowed ) return DefaultValueInfo.Disallowed;
                        Throw.DebugAssert( fInfo.RequiresInit );
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

            }

            public static void CheckInitialExchangeable( IActivityMonitor monitor, PocoType composite, IReadOnlyList<IPocoField> fields )
            {
                int exCount = 0;
                foreach( var f in fields )
                {
                    if( f.IsExchangeable ) exCount++;
                    else
                    {
                        monitor.Info( $"Field '{composite}.{f.Name}' is not exchangeable because its type '{f.Type}' is not exchangeable." );
                    }
                }
                if( exCount == 0 )
                {
                    composite.SetNotExchangeable( monitor, $"none of its {fields.Count} fields are exchangeable." );
                }
            }

            internal static void OnNoMoreExchangeable( IActivityMonitor monitor, PocoType composite, IReadOnlyList<IPocoField> fields, IPocoType.ITypeRef r )
            {
                int exCount = 0;
                foreach( var f in fields )
                {
                    if( f.IsExchangeable ) exCount++;
                    else if( f.Type == r.Type )
                    {
                        monitor.Info( $"Field '{composite}.{f.Name}' is no more exchangeable because its type '{f.Type}' is no more exchangeable." );
                    }
                }
                if( exCount == 0 )
                {
                    composite.SetNotExchangeable( monitor, $"no more fields are exchangeable." );
                }
            }
        }

    }
}
