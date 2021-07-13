using CK.CodeGen;
using System.Collections.Generic;
using System.Text;

namespace CK.Setup.Json
{
    /// <summary>
    /// Type handler with support for nullable types (value types as well as reference types)
    /// and abstract mapping.
    /// </summary>
    public interface IJsonCodeGenHandlerForReferenceType : IJsonCodeGenHandler
    {
        void GenerateReadFromUntyped( ICodeWriter read, string variableName, string castTypeName );
    }
}
