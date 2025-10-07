using CK.Core;
using System.Text.Encodings.Web;

namespace CK.Poco.Exc.Json;

/// <summary>
/// Describes standard forms for <see cref="UserMessage"/> Json serialization.
/// </summary>
public enum UserMessageSimplifiedFormat
{
    /// <summary>
    /// The full UserMessage form: a 8-cells array that includes every aspects of the message. This full form
    /// enables the UserMessage to be rewritten with another template (in another culture).
    /// <para>
    /// A Warning in the code-default with 2 placeholders, a depth of 37, with an associated "Test.Res" resource name that is
    /// translated only in "fr" as "S'il n'y pas {1}, alors il n'y a pas {0}." created in the current culture "fr-FR"
    /// (and written with the <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/>):
    /// <code>
    /// var msg = UserMessage.Warn( current, $"Concept {c1} requires {c2}.", resName: "Test.Res" ).With( 37 );
    /// </code>
    /// Produces this json:
    /// </para>
    /// <code>
    /// [8,37,"S'il n'y pas Animal, alors il n'y a pas Bird.","fr","Test.Res","Concept Bird requires Animal.","fr-fr",[8,4,22,6]]
    /// </code>
    /// </summary>
    None = 0,

    /// <summary>
    /// Keeps only the <see cref="SimpleUserMessage"/> information. The message from the <see cref="None"/> case is:
    /// <code>
    /// [8,"S'il n'y pas Animal, alors il n'y a pas Bird.",37]
    /// </code>
    /// </summary>
    Simple = 1,

    /// <summary>
    /// Only writes the "Level - Text" form. The <see cref="UserMessage.Depth"/> indents the Test.
    /// The message from the <see cref="None"/> case is (Depth is 37!):
    /// <code>
    /// "Warning -                                      S'il n'y pas Animal, alors il n'y a pas Bird."
    /// </code>
    /// </summary>
    String,
}
