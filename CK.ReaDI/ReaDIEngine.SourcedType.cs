using CK.Engine.TypeCollector;

namespace CK.Core;

public sealed partial class ReaDIEngine
{
    sealed class SourcedType
    {
        readonly ICachedType _sourceType;
        internal SourcedHandlerInstance? _firstInstance;
        internal bool _inactive;

        public SourcedType( ICachedType sourceType )
        {
            _sourceType = sourceType;
        }

        public ICachedType SourceType => _sourceType;

        /// <summary>
        /// Can be null if the source type has no attributes that are IReaDIHandler.
        /// </summary>
        public SourcedHandlerInstance? FirstInstance => _firstInstance;

        public override string ToString() => _sourceType.CSharpName;
    }

}

