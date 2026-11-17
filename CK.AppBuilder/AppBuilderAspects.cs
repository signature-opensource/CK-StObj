using CK.Core;
using CK.TypeCache;
using CK.TypeSet;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CK.Setup;

/// <summary>
/// Finds the aspects (<see cref="AppBuilderAspectAttribute"/>, <see cref="AppAspectAttribute"/>) in the type sets of the
/// Apps, creates them from their section of "ckonf.json" and applies them, before the contributors run.
/// </summary>
static class AppBuilderAspects
{
    public static bool Run( IActivityMonitor monitor, AppBuilderContext context, BinPathTypeGroup.Result sets )
    {
        using var _ = monitor.OpenInfo( "Applying the aspects." );
        var builderAspects = new Dictionary<string, (ICachedType Type, IConfigurableTypeAttribute Attribute, List<AppConfiguration> Apps)>( StringComparer.OrdinalIgnoreCase );
        var appAspects = new Dictionary<AppConfiguration, Dictionary<string, (ICachedType Type, IConfigurableTypeAttribute Attribute)>>();
        bool success = true;
        foreach( var g in sets.Groups )
        {
            if( g.IsUnifiedPure ) continue;
            var apps = context.Apps.Where( a => g.Configurations.Contains( a.BinPathConfiguration ) ).ToList();
            foreach( var t in g.ConfigurableTypes )
            {
                if( !t.TryGetConfigurableType( monitor, out var attr ) || attr == null ) continue;
                if( attr is AppBuilderAspectAttribute )
                {
                    if( !builderAspects.TryGetValue( attr.Name, out var known ) )
                    {
                        builderAspects.Add( attr.Name, (t, attr, new List<AppConfiguration>( apps )) );
                    }
                    else if( known.Type != t )
                    {
                        monitor.Error( $"The Builder aspect name '{attr.Name}' is declared by '{known.Type.CSharpName}' and '{t.CSharpName}': a name is unique." );
                        success = false;
                    }
                    else
                    {
                        known.Apps.AddRange( apps.Where( a => !known.Apps.Contains( a ) ) );
                    }
                }
                else if( attr is AppAspectAttribute )
                {
                    foreach( var a in apps )
                    {
                        if( !appAspects.TryGetValue( a, out var forApp ) ) appAspects.Add( a, forApp = new( StringComparer.OrdinalIgnoreCase ) );
                        if( forApp.TryGetValue( attr.Name, out var known ) && known.Type != t )
                        {
                            monitor.Error( $"The App aspect name '{attr.Name}' of {a} is declared by '{known.Type.CSharpName}' and '{t.CSharpName}': a name is unique." );
                            success = false;
                        }
                        else forApp[attr.Name] = (t, attr);
                    }
                }
            }
        }
        if( !success ) return false;
        foreach( var (name, (type, attr, apps)) in builderAspects.OrderBy( kv => kv.Key, StringComparer.OrdinalIgnoreCase ) )
        {
            if( !TryCreate<IAppBuilderAspect>( monitor, "Builder aspect", name, type, attr, context.Aspects, out var aspect ) )
            {
                success = false;
                continue;
            }
            if( aspect == null ) continue;
            using( monitor.OpenTrace( $"Builder aspect '{name}' ('{type.CSharpName}') for {apps.Select( a => a.ToString() ).Concatenate()}." ) )
            {
                success &= Call( monitor, name, () => aspect.Apply( monitor, context, apps ) );
            }
        }
        foreach( var (app, aspects) in appAspects.OrderBy( kv => kv.Key.Name, StringComparer.OrdinalIgnoreCase ) )
        {
            foreach( var (name, (type, attr)) in aspects.OrderBy( kv => kv.Key, StringComparer.OrdinalIgnoreCase ) )
            {
                if( !TryCreate<IAppAspect>( monitor, $"App aspect of {app}", name, type, attr, app.Aspects, out var aspect ) )
                {
                    success = false;
                    continue;
                }
                if( aspect == null ) continue;
                using( monitor.OpenTrace( $"App aspect '{name}' ('{type.CSharpName}') of {app}." ) )
                {
                    success &= Call( monitor, name, () => aspect.Apply( monitor, context, app ) );
                }
            }
        }
        return success;
    }

    // The aspect is present when its key is declared (true or its section) or when it is not optional, unless its key is
    // false (an error for a Required one). Returns false on error, the aspect is null when it is not present.
    static bool TryCreate<T>( IActivityMonitor monitor,
                              string kind,
                              string name,
                              ICachedType type,
                              IConfigurableTypeAttribute attr,
                              AppAspects aspects,
                              out T? aspect ) where T : class
    {
        aspect = null;
        var section = aspects.TryGetAspect( name );
        if( section != null && section.Value != null && bool.TryParse( section.Value, out var on ) && !on )
        {
            if( attr.Activation == TypeActivation.Required )
            {
                monitor.Error( $"The {kind} '{name}' is required: it can't be false." );
                return false;
            }
            return true;
        }
        if( section == null && attr.Activation == TypeActivation.Optional ) return true;
        if( !typeof( T ).IsAssignableFrom( type.Type ) )
        {
            monitor.Error( $"The {kind} '{name}' ('{type.CSharpName}') must be a {typeof( T ).Name}." );
            return false;
        }
        var ctors = type.Type.GetConstructors();
        var parameters = ctors.Length == 1 ? ctors[0].GetParameters() : null;
        if( parameters == null || parameters.Length > 1 || (parameters.Length == 1 && parameters[0].ParameterType != typeof( IConfiguration )) )
        {
            monitor.Error( $"The {kind} '{name}' ('{type.CSharpName}') must have one public constructor, without parameter or with its IConfiguration section." );
            return false;
        }
        // A bare "true" has no section.
        IConfiguration? configuration = section != null && section.Value == null ? section : null;
        try
        {
            aspect = (T)ctors[0].Invoke( parameters.Length == 0 ? [] : [configuration] );
            return true;
        }
        catch( TargetInvocationException ex ) when( ex.InnerException != null )
        {
            monitor.Error( $"Unable to create the {kind} '{name}' ({section?.Path ?? "no section"}): {ex.InnerException.Message}", ex.InnerException );
        }
        catch( Exception ex )
        {
            monitor.Error( $"Unable to create the {kind} '{name}' ({section?.Path ?? "no section"}).", ex );
        }
        return false;
    }

    static bool Call( IActivityMonitor monitor, string name, Func<bool> f )
    {
        try
        {
            return f();
        }
        catch( Exception ex )
        {
            monitor.Error( $"The aspect '{name}' failed.", ex );
            return false;
        }
    }
}
