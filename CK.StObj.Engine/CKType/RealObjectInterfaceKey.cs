using System;
using System.Diagnostics;

namespace CK.Setup
{
    /// <summary>
    /// Wrapper for keys in Type mapping dictionaries: when wrapped in this class,
    /// the Type is the key of its highest implementation instead of its final concrete class.
    /// This enables the use of one and only one dictionnary for Mappings (Type => Final Type) as well as 
    /// highest implementation association (Real object interface => its highest implementation).
    /// </summary>
    internal class RealObjectInterfaceKey
    {
        public readonly Type InterfaceType;

        public RealObjectInterfaceKey( Type ambientObjectInterface )
        {
            Debug.Assert( ambientObjectInterface.IsInterface );
            InterfaceType = ambientObjectInterface;
        }

        public override bool Equals( object obj )
        {
            RealObjectInterfaceKey k = obj as RealObjectInterfaceKey;
            return k != null && k.InterfaceType == InterfaceType;
        }

        public override int GetHashCode()
        {
            return -InterfaceType.GetHashCode();
        }
    }
}
