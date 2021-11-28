using CK.CodeGen;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Setup.Json
{
    /// <summary>
    /// The code writer delegate is in charge of generating the write code into a <see cref="System.Text.Json.Utf8JsonWriter"/>
    /// variable named "w" and a PocoJsonSerializerOptions variable named "options".
    /// <para>
    /// This is configured on JsonTypeInfo by <see cref="JsonTypeInfo.Configure(CodeWriter, CodeReader)"/> and
    /// used by handlers bound to the type when <see cref="JsonCodeGenHandler.GenerateWrite"/> is called.
    /// </para>
    /// </summary>
    /// <param name="write">The code writer to uses.</param>
    /// <param name="variableName">The variable name to write.</param>
    public delegate void CodeWriter( ICodeWriter write, string variableName );

    /// <summary>
    /// The code reader delegate is in charge of generating the read code from a  <see cref="System.Text.Json.Utf8JsonReader"/>
    /// variable named "r" and a PocoJsonSerializerOptions variable named "options".
    /// See <see cref="CodeWriter"/>.
    /// </summary>
    /// <param name="read">The code writer to use.</param>
    /// <param name="variableName">The variable name.</param>
    /// <param name="assignOnly">True is the variable must be only assigned: no in-place read is possible. Used by collections and Poco fields.</param>
    /// <param name="isNullable">True if the variable may initially be null, false if it is necessarily not null.</param>
    public delegate void CodeReader( ICodeWriter read, string variableName, bool assignOnly, bool isNullable );

}
