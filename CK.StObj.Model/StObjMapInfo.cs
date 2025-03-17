using System;
using System.Collections.Generic;
using System.Reflection;

#nullable enable

namespace CK.Core;

/// <summary>
/// Defines the key properties of available <see cref="IStObjMap"/> managed
/// by <see cref="StObjContextRoot"/>.
/// </summary>
public sealed class StObjMapInfo
{
    /// <summary>
    /// Gets the StObjMap generated signature.
    /// </summary>
    public SHA1Value GeneratedSignature { get; }

    /// <summary>
    /// Gets the names of the StObjMap.
    /// </summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>
    /// Gets the generated StObjMap type.
    /// </summary>
    public Type StObjMapType { get; }

    /// <summary>
    /// Gets the assembly name.
    /// </summary>
    public string AssemblyName { get; }

    /// <summary>
    /// This is managed by <see cref="StObjContextRoot.GetStObjMap(StObjMapInfo, IActivityMonitor?)"/>.
    /// </summary>
    internal IStObjMap? StObjMap;
    internal string? LoadError;

    /// <summary>
    /// Overridden to return the names, signature and assembly names.
    /// </summary>
    /// <returns>A readable string.</returns>
    public override string ToString() => $"Names: {Names.Concatenate()}, Signature: {GeneratedSignature}, Assembly: {AssemblyName}";

    StObjMapInfo( SHA1Value s, IReadOnlyList<string> n, Type t )
    {
        GeneratedSignature = s;
        Names = n;
        StObjMapType = t;
        AssemblyName = t.Assembly.GetName().Name!;
    }

    internal static StObjMapInfo? Create( IActivityMonitor m, Assembly a, CustomAttributeData attr )
    {
        try
        {
            object? v = attr.AttributeType.GetField( "V", BindingFlags.Public | BindingFlags.Static )?.GetValue( null );
            if( v == null ) m.Error( $"Unable to retrieve the CK.StObj.Signature assembly attribute from '{a.FullName}'." );
            else
            {
                (SHA1Value, IReadOnlyList<string>) s = ((SHA1Value, IReadOnlyList<string>))v;
                Type? t = a.GetType( StObjContextRoot.RootContextTypeFullName, false, false );
                if( t == null )
                {
                    m.Error( $"Unable to retrieve the generated {StObjContextRoot.RootContextTypeFullName} type from '{a.FullName}'." );
                }
                else
                {
                    var r = new StObjMapInfo( s.Item1, s.Item2, t );
                    m.Info( $"Found StObjMap: {r}." );
                    return r;
                }
            }

        }
        catch( Exception ex )
        {
            m.Error( "Unable to read StObjMap information.", ex );
        }
        return null;
    }
}
