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
/// Apps, creates them from their section of "ckonf.json" (with their options: <see cref="AppAspectOptionAttribute"/>,
/// <see cref="AppBuilderAspectOptionAttribute"/>) and applies them, before the contributors run.
/// </summary>
static class AppBuilderAspects
{
    const string _optionAttributeHint = "[AppAspectOption] for the options of an App aspect, [AppBuilderAspectOption] for the options of a Builder aspect";

    // An ordered option of an aspect, as the selection sees it.
    sealed record AspectOption( OrderedConfigurableOption Ordered ) : IConfigurationOption
    {
        public string Name => Ordered.Option.Name;
        public bool IsEnabledByDefault => Ordered.Option.Activation != TypeActivation.Optional;
        public bool IsRequired => Ordered.Option.Activation == TypeActivation.Required;
        public IReadOnlyList<string> Requires => Ordered.Requires;
    }

    public static bool Run( IActivityMonitor monitor, AppBuilderContext context, BinPathTypeGroup.Result sets )
    {
        using var _ = monitor.OpenInfo( "Applying the aspects." );
        var builderAspects = new Dictionary<string, (ICachedType Type, IConfigurableTypeAttribute Attribute, List<AppConfiguration> Apps)>( StringComparer.OrdinalIgnoreCase );
        var appAspects = new Dictionary<AppConfiguration, Dictionary<string, (ICachedType Type, IConfigurableTypeAttribute Attribute)>>();
        // The options of the aspects: the Builder aspects take the options of all the groups, an App aspect the ones of its group.
        var allOptions = new List<ConfigurableOptionModel>();
        var appOptions = new Dictionary<AppConfiguration, List<ConfigurableOptionModel>>();
        bool success = true;
        foreach( var g in sets.Groups )
        {
            if( g.IsUnifiedPure ) continue;
            var apps = context.Apps.Where( a => g.Configurations.Contains( a.BinPathConfiguration ) ).ToList();
            if( !ConfigurableOptions.TryCollect( monitor, g.ConfigurableTypes, [typeof( IAppAspect ), typeof( IAppBuilderAspect )], _optionAttributeHint, out var options ) )
            {
                success = false;
            }
            allOptions.AddRange( options.Where( o => !allOptions.Any( k => k.Type == o.Type ) ) );
            foreach( var a in apps ) appOptions[a] = options;
            foreach( var t in g.ConfigurableTypes )
            {
                if( !t.TryGetConfigurableType( monitor, out var attr ) || attr == null || attr is IConfigurableOptionAttribute ) continue;
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
            if( !TryCreate<IAppBuilderAspect>( monitor, context, sets.TypeCache, "Builder aspect", name, type, attr, context.Aspects, allOptions, out var aspect ) )
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
                if( !TryCreate<IAppAspect>( monitor, context, sets.TypeCache, $"App aspect of {app}", name, type, attr, app.Aspects, appOptions.GetValueOrDefault( app ) ?? [], out var aspect ) )
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
                              AppBuilderContext context,
                              GlobalTypeCache typeCache,
                              string kind,
                              string name,
                              ICachedType type,
                              IConfigurableTypeAttribute attr,
                              AppAspects aspects,
                              IReadOnlyList<ConfigurableOptionModel> options,
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
        // The constructor: its optional section and its optional options.
        var ctors = type.Type.GetConstructors();
        var parameters = ctors.Length == 1 ? ctors[0].GetParameters() : null;
        Type? optionsItemType = null;
        if( parameters != null )
        {
            foreach( var p in parameters )
            {
                if( p.ParameterType == typeof( IConfiguration ) ) continue;
                var item = GetOptionListItemType( p.ParameterType, type.Type );
                if( item == null || optionsItemType != null )
                {
                    parameters = null;
                    break;
                }
                optionsItemType = item;
            }
            if( parameters != null && parameters.Count( p => p.ParameterType == typeof( IConfiguration ) ) > 1 ) parameters = null;
        }
        if( parameters == null )
        {
            monitor.Error( $"The {kind} '{name}' ('{type.CSharpName}') must have one public constructor whose optional parameters are its IConfiguration section and its options (an IReadOnlyList of its option interface)." );
            return false;
        }
        // A bare "true" has no section.
        var aspectSection = section != null && section.Value == null ? section : null;
        // The options of the aspect: the ones of an owner type assignable from it, ordered, selected and created.
        Array? createdOptions = null;
        var applicable = options.Where( o => o.Owner.IsAssignableFrom( type.Type ) ).ToList();
        if( applicable.Count > 0 )
        {
            var ordered = ConfigurableOptions.TryOrder( monitor, typeCache, context.EngineConfiguration.RevertOrderingNames, type.Type, applicable );
            if( ordered == null ) return false;
            var infos = ordered.Select( o => new AspectOption( o ) ).ToList();
            if( !ConfigurationOptionSelector.TrySelect( monitor, infos, aspectSection, out var selected ) ) return false;
            if( selected.Count > 0 )
            {
                if( optionsItemType == null )
                {
                    monitor.Error( $"The {kind} '{name}' ('{type.CSharpName}') has enabled options ('{selected.Select( s => s.Option.Name ).Concatenate( "', '" )}') but its constructor doesn't receive them (an IReadOnlyList of its option interface)." );
                    return false;
                }
                createdOptions = Array.CreateInstance( optionsItemType, selected.Count );
                for( int i = 0; i < selected.Count; i++ )
                {
                    var (info, configuration) = selected[i];
                    if( !TryCreateOption( monitor, name, info.Ordered.Option, configuration, out var created ) ) return false;
                    if( !optionsItemType.IsInstanceOfType( created ) )
                    {
                        monitor.Error( $"The option '{info.Name}' ('{info.Ordered.Option.Type.CSharpName}') of the {kind} '{name}' is not a '{optionsItemType.ToCSharpName()}'." );
                        return false;
                    }
                    createdOptions.SetValue( created, i );
                }
            }
        }
        if( optionsItemType != null ) createdOptions ??= Array.CreateInstance( optionsItemType, 0 );
        var args = parameters.Select( p => p.ParameterType == typeof( IConfiguration ) ? (object?)aspectSection : createdOptions ).ToArray();
        try
        {
            aspect = (T)ctors[0].Invoke( args );
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

    // The item type of an IReadOnlyList<X> parameter where X is an option interface of the aspect (an
    // IConfigurableOption<TOwner> whose TOwner is assignable from the aspect type), null otherwise.
    static Type? GetOptionListItemType( Type parameterType, Type aspectType )
    {
        if( !parameterType.IsGenericType || parameterType.GetGenericTypeDefinition() != typeof( IReadOnlyList<> ) ) return null;
        var item = parameterType.GetGenericArguments()[0];
        var option = item.IsGenericType && item.GetGenericTypeDefinition() == typeof( IConfigurableOption<> )
                        ? item
                        : item.GetInterfaces().FirstOrDefault( i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof( IConfigurableOption<> ) );
        return option != null && option.GetGenericArguments()[0].IsAssignableFrom( aspectType ) ? item : null;
    }

    // An option of an aspect has one public constructor, without parameter or with its IConfiguration section.
    static bool TryCreateOption( IActivityMonitor monitor, string aspectName, ConfigurableOptionModel option, IConfiguration? configuration, out object created )
    {
        created = null!;
        var parameters = option.Constructor.GetParameters();
        if( parameters.Any( p => p.ParameterType != typeof( IConfiguration ) ) )
        {
            monitor.Error( $"The option '{option.Name}' ('{option.Type.CSharpName}') of the aspect '{aspectName}' must have one public constructor, without parameter or with its IConfiguration section." );
            return false;
        }
        try
        {
            created = option.Constructor.Invoke( parameters.Length == 0 ? [] : [configuration] );
            return true;
        }
        catch( TargetInvocationException ex ) when( ex.InnerException != null )
        {
            monitor.Error( $"Unable to create the option '{option.Name}' of the aspect '{aspectName}': {ex.InnerException.Message}", ex.InnerException );
        }
        catch( Exception ex )
        {
            monitor.Error( $"Unable to create the option '{option.Name}' of the aspect '{aspectName}'.", ex );
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
