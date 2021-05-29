#nullable enable

namespace CK.Setup
{
    public partial class PocoJsonSerializerImpl
    {
        /// <summary>
        /// Defines basic, direct types that are directly handled.
        /// </summary>
        public enum DirectType
        {
            /// <summary>
            /// Regular type.
            /// </summary>
            None,

            /// <summary>
            /// Untyped is handled by Read/WriteObject.
            /// </summary>
            Untyped,

            /// <summary>
            /// A raw string.
            /// </summary>
            String,

            /// <summary>
            /// A number is, by default, an integer.
            /// </summary>
            Int,

            /// <summary>
            /// Raw boolean type.
            /// </summary>
            Bool
        }

    }
}
