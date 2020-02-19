using System.Reflection;

namespace CK.Setup
{
    internal class InjectObjectInfo : AmbientPropertyOrInjectObjectInfo
    {
        public new readonly static string KindName = "[InjectObject]";
        
        internal InjectObjectInfo( PropertyInfo p, bool isOptionalDefined, bool isOptional, int definerSpecializationDepth, int index )
            : base( p, isOptionalDefined, isOptional, definerSpecializationDepth, index )
        {
        }

        public override string Kind => KindName; 
    }
}
