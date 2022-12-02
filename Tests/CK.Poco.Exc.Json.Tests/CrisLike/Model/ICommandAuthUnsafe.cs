
#pragma warning disable CS1574 // XML comment has cref attribute that could not be resolved

namespace CK.CrisLike
{
    /// <summary>
    /// Defines the <see cref="ActorId"/> field.
    /// This is the most basic command part: the only guaranty is that the actor identifier is the
    /// current <see cref="IAuthenticationInfo.UnsafeUser"/>.
    /// </summary>
    public interface ICommandAuthUnsafe : ICommandPart
    {
        /// <summary>
        /// Gets or sets the actor identifier.
        /// The default <see cref="CrisAuthenticationService"/> validates this field against the
        /// current <see cref="IAuthenticationInfo.UnsafeUser"/>.
        /// </summary>
        int ActorId { get; set; }
    }
}
