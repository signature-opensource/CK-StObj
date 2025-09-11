using System;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using CK.Core;

namespace CK.Setup;

/// <summary>
/// Mutable locator that combines a <see cref="Type"/> and a path to a resource. 
/// The <see cref="Path"/> can be <see cref="Merge"/>d with another one.
/// </summary>
/// <remarks>
/// The path may begin with a ~ and in such case, the resource path is "assembly based"
/// and the <see cref="Type"/> is used only for its assembly.
/// </remarks>
public sealed class ResourceLocator : IResourceLocator, IMergeable
{
    readonly Type _type;
    string? _path;

    /// <summary>
    /// Initializes a <see cref="ResourceLocator"/>.
    /// </summary>
    /// <param name="type">
    /// The assembly of this type must hold the resources. The <see cref="Type.Namespace"/>
    /// is the path prefix of the resources.
    /// </param>
    /// <param name="path">
    /// An optional sub path (can be null) from the namespace of the type to the resource 
    /// itself. Can be null or <see cref="String.Empty"/> if the resources are 
    /// directly associated to the type.
    /// </param>
    public ResourceLocator( Type type, string? path = null )
    {
        Throw.CheckNotNullArgument( type );
        _type = type;
        _path = path;
    }

    /// <summary>
    /// Gets the type that will be used to locate the resource.
    /// </summary>
    public Type Type => _type;

    /// <summary>
    /// Gets or sets a sub path from the namespace of the <see cref="Type"/> to the resources.
    /// Can be null (or <see cref="String.Empty"/>) if the resources are directly 
    /// associated to the type.
    /// <para>
    /// When null, this path can be initialized by a <see cref="Merge(object, IServiceProvider)"/>.
    /// </para>
    /// </summary>
    public string? Path
    {
        get => _path;
        set => _path = value;
    }

    /// <summary>
    /// Compute the resource full name from the namespace of the <see cref="Type"/> 
    /// and the <see cref="Path"/> properties and combines it with the given name.
    /// </summary>
    /// <param name="name">Name of the resource. Usually a file name ('sProc.sql')</param>
    /// <returns>The full resource name.</returns>
    public string GetResourceName( string? name )
    {
        return GetResourceName( Type, _path, name );
    }

    /// <summary>
    /// Gets an ordered list of resource names that starts with the <paramref name="namePrefix"/>.
    /// </summary>
    /// <param name="namePrefix">Prefix for any strings.</param>
    /// <returns>
    /// Ordered lists of available resource names (with the <paramref name="namePrefix"/>). 
    /// Resource content can then be obtained by <see cref="OpenStream(string?, bool)"/> or <see cref="GetString(string?, bool, string[])"/>.
    /// </returns>
    public IEnumerable<string> GetNames( string namePrefix )
    {
        IReadOnlyList<string> a = Type.Assembly.GetSortedResourceNames();

        string p = GetResourceName( "." );
        namePrefix = p + namePrefix;

        // TODO: Use the fact that the list is sorted to 
        // select the sub range instead of that Where linear filter.           
        return a.Where( n => n.Length > namePrefix.Length && n.StartsWith( namePrefix, StringComparison.Ordinal ) )
                .Select( n => n.Substring( p.Length ) );
    }

    /// <summary>
    /// Obtains the content of a resource.
    /// </summary>
    /// <param name="name">Name of the resource to load.</param>
    /// <param name="throwError">
    /// When set to false, no exception will be thrown if the resource 
    /// does not exist and null is returned.
    /// </param>
    /// <returns>
    /// An opened <see cref="Stream"/> if the resource is found.
    /// Null if the resource is not found and <paramref name="throwError"/> is false.
    /// </returns>
    public Stream? OpenStream( string? name, bool throwError )
    {
        return OpenStream( Type, _path, name, throwError );
    }

    /// <summary>
    /// Obtains the content of a resource.
    /// </summary>
    /// <param name="name">Name of the resource to load.</param>
    /// <returns>
    /// An opened <see cref="Stream"/> if the resource is found.
    /// </returns>
    public Stream? OpenRequiredStream( string? name ) => OpenStream( Type, _path, name, throwError: true );

    /// <summary>
    /// Obtains the content of a resource as a string.
    /// </summary>
    /// <param name="name">
    /// Name of the resource. Usually a file name ('sProc.sql') or a type ('Model.User.1.0.0.sql') but can be any suffix.
    /// </param>
    /// <param name="throwError">
    /// Set to false, no exception will be thrown if the resource does not exist.
    /// </param>
    /// <param name="allowedNamePrefix">
    /// Allowed prefixes like "[Replace]" or "[Override]".
    /// </param>
    /// <returns>
    /// A string (possibly empty) if the resource is found.
    /// Null if the resource is not found and <paramref name="throwError"/> is false.
    /// </returns>
    public string? GetString( string? name, bool throwError, params string[] allowedNamePrefix )
    {
        foreach( var p in allowedNamePrefix )
        {
            if( string.IsNullOrEmpty( p ) ) continue;
            string? s = LoadString( Type, _path, p + name, false );
            if( s != null ) return s;
        }
        return LoadString( Type, _path, name, throwError );
    }

    /// <summary>
    /// Obtains the content of a resource as a string or throws a <see cref="CKException"/>.
    /// </summary>
    /// <param name="name">
    /// Name of the resource. Usually a file name ('sProc.sql') or a type ('Model.User.1.0.0.sql') but can be any suffix.
    /// </param>
    /// <param name="allowedNamePrefix">
    /// Allowed prefixes like "[Replace]" or "[Override]".
    /// </param>
    /// <returns>A string (possibly empty).</returns>
    public string? GetRequiredString( string? name, params string[] allowedNamePrefix ) => GetString( name, true, allowedNamePrefix );

    /// <summary>
    /// Obtains the content of a resource as a string.
    /// </summary>
    /// <param name="name">
    /// Name of the resource (can be null or empty). Usually a file name ('sProc.sql') or a type ('Model.User.1.0.0.sql') but can be any suffix.
    /// </param>
    /// <param name="throwError">
    /// Set to false, no exception will be thrown if the resource does not exist.
    /// </param>
    /// <param name="namePrefix">
    /// The prefix found (prefix are looked up first). String.Empty if there were no match with prefix.
    /// </param>
    /// <param name="allowedNamePrefix">
    /// Allowed prefixes like "[Replace]" or "[Override]".
    /// </param>
    /// <returns>
    /// A string (possibly empty) if the resource is found.
    /// Null if the resource is not found and <paramref name="throwError"/> is false.
    /// </returns>
    public string? GetString( string? name, bool throwError, out string namePrefix, params string[] allowedNamePrefix )
    {
        foreach( var p in allowedNamePrefix )
        {
            string? s = LoadString( Type, _path, p + name, false );
            if( s != null )
            {
                namePrefix = p;
                return s;
            }
        }
        namePrefix = String.Empty;
        return LoadString( Type, _path, name, throwError );
    }

    /// <summary>
    /// Overridden to return the assembly:path for this ResourceLocator.
    /// </summary>
    /// <returns>The "assembly:path:*" string.</returns>
    public override string ToString() => $"{_type.Assembly.GetName().Name}:{GetResourceName( null )}";

    /// <summary>
    /// Computes the resource full name from the namespace of the <paramref name="resourceHolder"/> 
    /// and the <paramref name="path"/> (that can be null or empty).
    /// </summary>
    /// <param name="resourceHolder">
    /// The assembly of this type must hold the resources and its <see cref="Type.Namespace"/>
    /// is the path prefix of the resources.
    /// </param>
    /// <param name="path">
    /// An optional sub path from the namespace of the type to the resource 
    /// itself. Can be null or <see cref="String.Empty"/> if the resources are 
    /// directly associated to the namespace of the type.
    /// </param>
    /// <param name="name">
    /// Name of the resource (can be null or empty). Usually a file name ('sProc.sql') or a type ('Model.User.1.0.0.sql') but can be any suffix.
    /// When not null nor empty a '.' will automatically be inserted between <paramref name="path"/> and name.
    /// </param>
    /// <returns>The full resource name.</returns>
    public static string GetResourceName( Type resourceHolder, string? path, string? name )
    {
        Throw.CheckNotNullArgument( resourceHolder );
        Throw.CheckArgument( resourceHolder.Namespace is not null );
        string ns = resourceHolder.Namespace;
        if( path != null && path.Length != 0 )
        {
            if( path[0] == '~' )
            {
                if( path.Length > 1 && path[1] == '.' )
                    path = path.Substring( 2 );
                else path = path.Substring( 1 );
            }
            else
            {
                if( path[0] == '.' )
                    path = ns + path;
                else path = ns + '.' + path;
            }
        }
        else path = ns;
        if( name == null || name.Length == 0 ) return path;
        if( name[0] == '.' )
        {
            if( path.Length > 0 && path[path.Length - 1] == '.' ) return path + name.Substring( 1 );
            return path + name;
        }
        if( path.Length > 0 && path[path.Length - 1] == '.' ) return path + name;
        return path + '.' + name;
    }

    /// <summary>
    /// Obtains the content of a resource.
    /// </summary>
    /// <exception cref="ApplicationException">If <paramref name="throwError"/> is true and the resource can not be found.</exception>
    /// <param name="resourceHolder">
    /// The assembly of this type must hold the resources and its <see cref="Type.Namespace"/>
    /// is the path prefix of the resources.
    /// </param>
    /// <param name="path">
    /// A sub path from the namespace of the type to the resource 
    /// itself. Can be null or <see cref="String.Empty"/> if the resources are 
    /// directly associated to the type.
    /// </param>
    /// <param name="name">Name of the resource to load.</param>
    /// <param name="throwError">
    /// When set to false, no exception will be thrown if the resource 
    /// does not exist.
    /// </param>
    /// <returns>
    /// An opened <see cref="Stream"/> if the resource is found.
    /// Null if the resource is not found and <paramref name="throwError"/> is false.
    /// </returns>
    public static Stream? OpenStream( Type resourceHolder, string? path, string? name, bool throwError )
    {
        string fullName = GetResourceName( resourceHolder, path, name );
        return OpenStream( resourceHolder.Assembly, fullName, name, throwError );
    }

    /// <summary>
    /// Obtains the content of a resource as a string from the <paramref name="resourceHolder"/>'s assembly.
    /// </summary>
    /// <param name="resourceHolder">
    /// The assembly of this type must hold the resources its <see cref="T:Type.Namespace"/>
    /// is the path prefix of the resources.
    /// </param>
    /// <param name="path">
    /// A sub path from the namespace of the type to the resource 
    /// itself. Can be null or <see cref="String.Empty"/> if the resources are 
    /// directly associated to the type.
    /// </param>
    /// <param name="name">Name of the resource to load.</param>
    /// <param name="throwError">
    /// When set to false, no exception will be thrown if the resource 
    /// does not exist.
    /// </param>
    /// <returns>A string (possibly empty) if the resource is found.
    /// Null if the resource is not found and <paramref name="throwError"/> is false.
    /// </returns>
    public static string? LoadString( Type resourceHolder, string? path, string? name, bool throwError )
    {
        string fullName = GetResourceName( resourceHolder, path, name );
        using( Stream? stream = OpenStream( resourceHolder.Assembly, fullName, name, throwError ) )
        {
            if( stream == null ) return null;
            using( StreamReader reader = new StreamReader( stream, true ) )
            {
                return reader.ReadToEnd();
            }
        }
    }

    static Stream? OpenStream( Assembly a, string fullResName, string? name, bool throwError )
    {
        Stream? stream = a.GetManifestResourceStream( fullResName );
        if( stream == null && throwError )
        {
            string? shouldBe = null;
            if( name != null )
            {
                var resNames = a.GetSortedResourceNames();
                foreach( string s in resNames )
                {
                    if( s.IndexOf( name, StringComparison.OrdinalIgnoreCase ) >= 0 )
                    {
                        shouldBe = s;
                        break;
                    }
                }
            }
            throw new CKException( $"Resource not found: '{fullResName}' in assembly '{a.GetName().Name}'.{(shouldBe == null ? string.Empty : $" It seems to be '{shouldBe}'.")}" );
        }
        return stream;
    }

    /// <summary>
    /// Merges information from another <see cref="IResourceLocator"/>: if this <see cref="Path"/>
    /// is null, is is set.
    /// When this Path starts with a dot, it is appended to the path of the merged object.
    /// </summary>
    /// <param name="source">ResourceLocator to combine with this one.</param>
    /// <param name="services">Optional services (not used by this implementation).</param>
    /// <returns>True on success, false otherwise.</returns>
    public bool Merge( object source, IServiceProvider? services = null )
    {
        if( source is IResourceLocator r )
        {
            if( _path == null ) _path = r.Path;
            else if( r.Path != null && _path.Length > 0 && _path[0] == '.' )
            {
                _path = r.Path + _path;
            }
            return true;
        }
        return false;
    }
}
