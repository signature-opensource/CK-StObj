using System;
using System.Collections.Generic;

namespace CK.Core;

/// <summary>
/// Defines a set of annotations.
/// <para>
/// This is directly inspired by <see cref="System.Xml.Linq.XObject"/> annotations.
/// </para>
/// </summary>
public interface IAnnotationSet
{
    /// <summary>
    /// Adds an object to the annotation list.
    /// </summary>
    /// <param name="annotation">The annotation to add.</param>
    void AddAnnotation( object annotation );

    /// <summary>
    /// Returns the first annotation object of the specified type from the list of annotations.
    /// </summary>
    /// <param name="type">The type of the annotation to retrieve.</param>
    /// <returns>
    /// The first matching annotation object, or null if no annotation is the specified type.
    /// </returns>
    object? Annotation( Type type );

    /// <summary>
    /// Returns the first annotation object of the specified type from the list of annotations.
    /// </summary>
    /// <typeparam name="T">The type of the annotation to retrieve.</typeparam>
    /// <returns>
    /// The first matching annotation object, or null if no annotation
    /// is the specified type.
    /// </returns>
    T? Annotation<T>() where T : class;

    /// <summary>
    /// Returns an enumerable collection of annotations of the specified type.
    /// </summary>
    /// <param name="type">The type of the annotations to retrieve.</param>
    /// <returns>An enumerable collection of annotations for this XObject.</returns>
    IEnumerable<object> Annotations( Type type );

    /// <summary>
    /// Returns an enumerable collection of annotations of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the annotations to retrieve.</typeparam>
    /// <returns>An enumerable collection of annotations for this XObject.</returns>
    IEnumerable<T> Annotations<T>() where T : class;

    /// <summary>
    /// Removes the annotations of the specified type.
    /// </summary>
    /// <param name="type">The type of annotations to remove.</param>
    void RemoveAnnotations( Type type );

    /// <summary>
    /// Removes the annotations of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of annotations to remove.</typeparam>
    void RemoveAnnotations<T>() where T : class;
}
