using CK.Core;

namespace CK.CrisLike
{
    [CKTypeSuperDefiner]
    public interface ICrisPoco : IPoco
    {
        /// <summary>
        /// Gets the <see cref="ICrisPocoModel"/> that describes this command.
        /// This property is automatically implemented.
        /// ...By Cris... Not here.
        /// </summary>
        //[AutoImplementationClaim]
        //ICrisPocoModel CrisPocoModel { get; }
    }

}
