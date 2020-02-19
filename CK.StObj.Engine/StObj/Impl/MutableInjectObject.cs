using System;

namespace CK.Setup
{
    /// <summary>
    /// Describes an Ambient singleton property.
    /// </summary>
    internal class MutableInjectObject : MutableReferenceOptional, IStObjMutableInjectObject
    {
        internal readonly InjectObjectInfo InjecttInfo;

        internal MutableInjectObject( MutableItem owner, InjectObjectInfo info )
            : base( owner, StObjMutableReferenceKind.RealObject )
        {
            InjecttInfo = info;
            Type = InjecttInfo.PropertyType;
            IsOptional = InjecttInfo.IsOptional;
        }

        public override string Name => InjecttInfo.Name;

        internal override string KindName => "InjectObject";

        internal override Type UnderlyingType => InjecttInfo.PropertyType;

        public override string ToString() => $"Inject Object '{Name}' of '{Owner}'";

    }
}
