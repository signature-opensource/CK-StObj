using CK.Engine.TypeCollector;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace CK.Core;

public sealed partial class ReaDIEngine
{
    sealed class LoopParameterType
    {
        readonly LoopTree? _tree;
        readonly ICachedType _type;
        ICachedType _loopStateType;
        ParameterType? _parameter;

        LoopParameterType? _parent;
        internal LoopParameterType? _firstChild;
        internal LoopParameterType? _next;

        internal LoopParameterType( LoopTree tree, ICachedType type, LoopParameterType? parent )
        {
            _tree = tree;
            _type = type;
            _loopStateType = tree.TypeCache.KnownTypes.Void;
            if( parent != null )
            {
                _parent = parent;
                _next = parent._firstChild;
                parent._firstChild = this;
            }
        }

        public ICachedType Type => _type;

        public LoopParameterType? Parent => _parent;

        public bool HasChildren => _firstChild != null;

        [MemberNotNullWhen(true,nameof(Parameter))]
        public bool HasParameter => _parameter != null;

        public ParameterType? Parameter => _parameter;

        public ICachedType LoopStateType => _loopStateType;

        internal void SetFirstParameter( ParameterType p, ICachedType loopStateType )
        {
            Throw.DebugAssert( !HasParameter );
            _parameter = p;
            SetLoopStateType( loopStateType );
        }

        internal void SetLoopStateType( ICachedType loopStateType )
        {
            Throw.DebugAssert( "Can only transition from a void to a typed state.", _loopStateType.Type == typeof(void) );
            _loopStateType = loopStateType;
        }

        internal static void GetLoopParameterAttributeValues( ICachedType type, out bool isRoot, out System.Type? parentType )
        {
            var attributes = type.AttributesData;
            isRoot = attributes.Any( a => a.AttributeType == typeof( ReaDILoopRootParameterAttribute ) );
            parentType = attributes.FirstOrDefault( a => a.AttributeType == typeof( ReaDILoopParameterAttribute<> ) )?.AttributeType.GetGenericArguments()[0];
        }

    }
}

