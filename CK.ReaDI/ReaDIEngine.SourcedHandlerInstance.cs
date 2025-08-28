namespace CK.Core;

public sealed partial class ReaDIEngine
{

    sealed class SourcedHandlerInstance
    {
        readonly IReaDIHandler _handler;
        readonly SourcedType _sourcedType;
        readonly SourcedHandlerInstance? _nextInHandlerType;
        readonly SourcedHandlerInstance? _nextInSourcedType;

        public SourcedHandlerInstance( IReaDIHandler handler, SourcedType sourcedType, SourcedHandlerInstance? nextInHandlerType )
        {
            _handler = handler;
            _sourcedType = sourcedType;
            _nextInHandlerType = nextInHandlerType;
            _nextInSourcedType = sourcedType.FirstInstance;
            sourcedType._firstInstance = this;
        }

        public IReaDIHandler Handler => _handler;

        public SourcedHandlerInstance? NextInHandlerType => _nextInHandlerType;

        public SourcedHandlerInstance? NextInSourcedType => _nextInSourcedType;
    }

}

