using System;

namespace CK.Core;

[AttributeUsage(AttributeTargets.Class|AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class CKAbstractAttribute : Attribute, ICKAbstractAttribute
{

}
