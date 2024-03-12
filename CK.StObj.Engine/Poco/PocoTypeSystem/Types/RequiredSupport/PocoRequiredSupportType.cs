using CK.Core;
using System.Diagnostics.CodeAnalysis;

namespace CK.Setup
{
    abstract class PocoRequiredSupportType : IPocoRequiredSupportType
    {
        [AllowNull]
        IPocoType _supportedType;
        readonly bool _isGenerated;

        // For generated type.
        protected PocoRequiredSupportType( string typeName )
            : this( IPocoRequiredSupportType.Namespace, typeName )
        {
            _isGenerated = true;
        }

        // For existing type.
        protected PocoRequiredSupportType( string @namespace, string typeName )
        {
            Throw.DebugAssert( !string.IsNullOrWhiteSpace( @namespace ) );
            Throw.DebugAssert( !string.IsNullOrWhiteSpace( typeName ) );
            TypeName = typeName;
            FullName = $"{@namespace}.{typeName}";
        }

        public bool IsGenerated => _isGenerated;

        public string FullName { get; }

        public string TypeName { get; }

        public IPocoType SupportedType => _supportedType;

        internal void SetSupportedType( IPocoType supportedType )
        {
            // Why checking this?
            // Because this is why this SupportedType has been introduced: we can then know the Type that
            // is a final type (mainly for serialization) from the NonNullableFinalTypes Dictionary<Type,int> that is
            // generated.
            // Without this link to the supported type we would have to type match the Type at runtime.
            Throw.DebugAssert( "The supported type's oblivious type is necessarily final.", supportedType.ObliviousType.IsNonNullableFinalType );
            _supportedType = supportedType;
        }
    }
}
