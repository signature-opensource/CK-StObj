using CK.Core;

namespace CK.Setup
{
    abstract class PocoRequiredSupportType : IPocoRequiredSupportType
    {
        // For generated type.
        protected PocoRequiredSupportType( string typeName )
        {
            Throw.DebugAssert( !string.IsNullOrWhiteSpace( typeName ) );
            TypeName = typeName;
            FullName = $"{IPocoRequiredSupportType.Namespace}.{typeName}";
        }

        public string FullName { get; }

        public string TypeName { get; }
    }
}
