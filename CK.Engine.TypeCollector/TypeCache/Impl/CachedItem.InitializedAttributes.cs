using CK.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace CK.Engine.TypeCollector;

abstract partial class CachedItem
{
    public bool TryGetInitializedAttributes( IActivityMonitor monitor, out ImmutableArray<object> attributes )
    {
        return _finalAttributesInitialized
                ? (attributes = _finalAttributes).IsDefault is false
                : TryInitializeAttributes( monitor, this, RawAttributes, ref _finalAttributesInitialized, ref _finalAttributes, out attributes );
    }

    internal static bool TryInitializeAttributes( IActivityMonitor monitor,
                                                  ICachedItem decoratedItem,
                                                  ImmutableArray<object> rawAttributes,
                                                  ref bool initialized,
                                                  ref ImmutableArray<object> cachedAttributes,
                                                  out ImmutableArray<object> attributes )
    {
        initialized = true;
        if( rawAttributes.Length == 0 )
        {
            cachedAttributes = attributes = rawAttributes;
            return true;
        }
        bool isTypeAttributes = decoratedItem is CachedType;
        attributes = default;
        for( int i = 0; i < rawAttributes.Length; i++ )
        {
            var attr = rawAttributes[i];
            if( isTypeAttributes )
            {
                if( attr is PrimaryTypeAttribute or SecondaryTypeAttribute )
                {
                    if( !TryInitialize( monitor, decoratedItem, rawAttributes, i, isTypeAttributes, out attributes ) )
                    {
                        return false;
                    }
                    break;
                }
            }
            else
            {
                if( attr is PrimaryMemberAttribute or SecondaryMemberAttribute )
                {
                    if( !TryInitialize( monitor, decoratedItem, rawAttributes, i, isTypeAttributes, out attributes ) )
                    {
                        return false;
                    }
                    break;
                }
            }
        }
        cachedAttributes = attributes;
        return true;

        static bool TryInitialize( IActivityMonitor monitor,
                                   ICachedItem decoratedItem,
                                   ImmutableArray<object> rawAttributes,
                                   int firstAttrIndex,
                                   bool isTypeAttributes,
                                   out ImmutableArray<object> attributes )
        {
            bool success = true;
            var result = rawAttributes.ToArray();
            IPrimaryAttributeImpl? currentPrimary = null;
            ISecondaryAttributeImpl? currentSecondary = null;
            bool instantiateSuccess = true;
            bool secondaryBindingFound = true;
            for( int i = firstAttrIndex; i < rawAttributes.Length; i++ )
            {
                success &= ReplaceByAttributeImpl( monitor,
                                                   decoratedItem,
                                                   isTypeAttributes,
                                                   ref result[i],
                                                   ref currentPrimary,
                                                   ref currentSecondary,
                                                   ref instantiateSuccess,
                                                   ref secondaryBindingFound );
            }
            if( success )
            {
                Throw.DebugAssert( instantiateSuccess && secondaryBindingFound );
                success = InitializeAttributes( monitor, decoratedItem, firstAttrIndex, result );
            }
            else if( instantiateSuccess && !secondaryBindingFound )
            {
                LogSecondaryBindingError( monitor, decoratedItem, firstAttrIndex, result );
            }
            if( success )
            {
                attributes = ImmutableCollectionsMarshal.AsImmutableArray( result );
            }
            else
            {
                attributes = default;
                monitor.Fatal( $"Failed to initialize Attributes on {decoratedItem}." );
            }
            return success;

            static bool ReplaceByAttributeImpl( IActivityMonitor monitor,
                                                ICachedItem decoratedItem,
                                                bool isTypeAttributes,
                                                ref object attr,
                                                ref IPrimaryAttributeImpl? currentPrimary,
                                                ref ISecondaryAttributeImpl? currentSecondary,
                                                ref bool instantiateSuccess,
                                                ref bool secondaryBindingFound )
            {
                if( isTypeAttributes )
                {
                    if( attr is PrimaryTypeAttribute primary )
                    {
                        var p = (IPrimaryAttributeImpl?)Instantiate( monitor,
                                                                     decoratedItem,
                                                                     primary,
                                                                     primary.ActualAttributeTypeAssemblyQualifiedName,
                                                                     typeof( PrimaryTypeAttributeImpl ),
                                                                     ref instantiateSuccess );
                        if( p == null ) return false;
                        currentPrimary = p;
                        currentSecondary = null;
                        attr = p;
                        return true;
                    }
                    if( attr is SecondaryTypeAttribute secondary )
                    {
                        var s = (ISecondaryAttributeImpl?)Instantiate( monitor,
                                                                       decoratedItem,
                                                                       secondary,
                                                                       secondary.ActualAttributeTypeAssemblyQualifiedName,
                                                                       typeof( SecondaryTypeAttributeImpl ),
                                                                       ref instantiateSuccess );
                        return BindSecondary( monitor,
                                              s,
                                              ref attr,
                                              currentPrimary,
                                              ref currentSecondary,
                                              ref instantiateSuccess,
                                              ref secondaryBindingFound );
                    }
                }
                else
                {
                    if( attr is PrimaryMemberAttribute primary )
                    {
                        var p = (IPrimaryAttributeImpl?)Instantiate( monitor,
                                                                     decoratedItem,
                                                                     primary,
                                                                     primary.ActualAttributeTypeAssemblyQualifiedName,
                                                                     typeof( PrimaryMemberAttributeImpl ),
                                                                     ref instantiateSuccess );
                        if( p == null ) return false;
                        currentPrimary = p;
                        currentSecondary = null;
                        attr = p;
                        return true;
                    }
                    else if( attr is SecondaryMemberAttribute secondary )
                    {
                        var s = (ISecondaryAttributeImpl?)Instantiate( monitor,
                                                                       decoratedItem,
                                                                       secondary,
                                                                       secondary.ActualAttributeTypeAssemblyQualifiedName,
                                                                       typeof( SecondaryMemberAttributeImpl ),
                                                                       ref instantiateSuccess );
                        return BindSecondary( monitor,
                                              s,
                                              ref attr,
                                              currentPrimary,
                                              ref currentSecondary,
                                              ref instantiateSuccess,
                                              ref secondaryBindingFound );
                    }
                }
                return true;

                static bool BindSecondary( IActivityMonitor monitor,
                                           ISecondaryAttributeImpl? secondary,
                                           ref object attr,
                                           IPrimaryAttributeImpl? currentPrimary,
                                           ref ISecondaryAttributeImpl? currentSecondary,
                                           ref bool instantiateSuccess,
                                           ref bool secondaryBindingFound )
                {
                    if( secondary == null ) return false;
                    var pType = secondary.ExpectedPrimaryType;
                    if( !CheckAttributeSuffix( monitor, pType ) )
                    {
                        instantiateSuccess = false;
                        return false;
                    }
                    attr = secondary;
                    if( currentPrimary == null || !pType.IsAssignableFrom( currentPrimary.Attribute.GetType() ) )
                    {
                        secondaryBindingFound = false;
                        return false;
                    }
                    var lastSecondary = currentSecondary;
                    currentSecondary = secondary;
                    return secondary.SetPrimary( monitor, currentPrimary, lastSecondary );
                }
            }

            static bool CheckAttributeSuffix( IActivityMonitor monitor, Type tAttr )
            {
                var tName = tAttr.Name.AsSpan();
                if( tName.Length <= 9 || !tName[^9..].SequenceEqual( "Attribute" ) )
                {
                    monitor.Error( $"""
                        Attribute name '{tAttr:N}' is invalid: it must be suffixed by "Attribute".
                        """ );
                    return false;
                }
                return true;
            }

            static IAttributeImpl? Instantiate( IActivityMonitor monitor,
                                                ICachedItem decoratedItem,
                                                Attribute attr,
                                                string aqn,
                                                Type expectedType,
                                                ref bool instantiateSuccess )
            {
                if( !CheckAttributeSuffix( monitor, attr.GetType() ) )
                {
                    instantiateSuccess = false;
                    return null;
                }
                try
                {
                    var type = Type.GetType( aqn, throwOnError: true, ignoreCase: false );
                    if( !expectedType.IsAssignableFrom( type ) )
                    {
                        monitor.Fatal( $"""
                            Invalid engine type resolution: '{aqn}' for:
                            [{attr.GetType().Name.AsSpan( ..^9 )}]
                            {decoratedItem}
                            The Attribute implementation type must be a '{expectedType:N}'.
                            """ );
                        instantiateSuccess = false;
                        return null;
                    }
                    var o = (IAttributeImpl)Activator.CreateInstance( type )!;
                    o.InitFields( monitor, decoratedItem, attr );
                    return o;
                }
                catch( Exception ex )
                {
                    monitor.Fatal( $"""
                        While creating attribute implementation '{aqn}' for:
                        [{attr.GetType().Name.AsSpan( ..^9 )}]
                        {decoratedItem}
                        """, ex );
                    instantiateSuccess = false;
                    return null;
                }
            }

            static bool InitializeAttributes( IActivityMonitor monitor,
                                              ICachedItem decoratedItem,
                                              int firstAttrIndex,
                                              object[] result )
            {
                bool success = true;
                for( int i = firstAttrIndex; i < result.Length; i++ )
                {
                    if( result[i] is IPrimaryAttributeImpl p )
                    {
                        try
                        {
                            success &= p.Initialize( monitor );
                        }
                        catch( Exception ex )
                        {
                            monitor.Error( $"While initializing '{p:N}' from {decoratedItem}.", ex );
                            success = false;
                        }
                    }
                }
                if( success )
                {
                    for( int i = firstAttrIndex; i < result.Length; i++ )
                    {
                        if( result[i] is IAttributeImpl p )
                        {
                            try
                            {
                                success &= p.OnInitialized( monitor );
                            }
                            catch( Exception ex )
                            {
                                monitor.Error( $"While calling '{p:N}.OnInitialized' from {decoratedItem}.", ex );
                                success = false;
                            }
                        }
                    }
                }
                return success;
            }
        }

    }

    private static void LogSecondaryBindingError( IActivityMonitor monitor,
                                                  ICachedItem decoratedItem,
                                                  int firstAttrIndex,
                                                  object[] result )
    {
        using( monitor.OpenError( "Unable to initialize Primary/Secondary attributes:" ) )
        {
            StringBuilder b = new StringBuilder();
            Type? lastExpectedPrimaryType = null;
            int primaryNumber = 0;
            for( int i = firstAttrIndex; i < result.Length; i++ )
            {
                var attr = result[i];
                if( attr is IPrimaryAttributeImpl p )
                {
                    b.Append( '[' ).Append( p.AttributeName ).Append( "] (primary n°" ).Append( ++primaryNumber ).Append( ')' );
                    lastExpectedPrimaryType = null;
                }
                else if( attr is ISecondaryAttributeImpl s )
                {
                    if( s.Primary == null )
                    {
                        WriteBindingError( b, s, result, ref lastExpectedPrimaryType );
                    }
                    else
                    {
                        b.Append( '[' ).Append( s.AttributeName ).Append( ']' );
                    }
                }
                b.AppendLine();
            }
            decoratedItem.Write( b );
            monitor.Trace( b.ToString() );
        }

        static void WriteBindingError( StringBuilder b,
                                       ISecondaryAttributeImpl secondary,
                                       object[] attributes,
                                       ref Type? lastExpectedPrimaryType )
        {
            b.Append( "**[" ).Append( secondary.AttributeName ).Append( "]**" );
            if( lastExpectedPrimaryType != null
                && secondary.ExpectedPrimaryType.IsAssignableFrom( lastExpectedPrimaryType ) )
            {
                b.Append( " (Same as above.)" );
            }
            else
            {
                var possible = attributes.OfType<IPrimaryAttributeImpl>().Select( ( p, idx ) => (p, idx) )
                                     .Where( v => secondary.ExpectedPrimaryType.IsAssignableFrom( v.p.Attribute.GetType() ) );
                int count = 0;
                foreach( var v in possible )
                {
                    if( ++count > 1 ) b.Append( " or " );
                    else b.Append( " Must be after " );
                    b.Append( '[' ).Append( v.p.AttributeName ).Append( "] (n°" ).Append( v.idx + 1 ).Append( ')' );
                }
                if( count == 0 )
                {
                    b.Append( " Missing a [" )
                     .Append( secondary.ExpectedPrimaryType.Name.AsSpan( ..^9 ) )
                     .Append( "] primary attribute (or a specialization) above this one" );
                }
                b.Append( '.' );
            }
            lastExpectedPrimaryType = secondary.ExpectedPrimaryType;
        }
    }
}
