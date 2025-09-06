using System;

namespace CK.Core;

/// <summary>
/// Enables any selected type to indicate that another type must also be considered.
/// </summary>
/// <typeparam name="T">The other type that must be considered.</typeparam>
[AttributeUsage( AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct|AttributeTargets.Enum,
                 AllowMultiple = true,
                 Inherited = false )]
public sealed class AlsoRegisterTypeAttribute<T> : Attribute
{
}

/// <summary>
/// Enables any selected type to indicate that other types must also be considered.
/// </summary>
/// <typeparam name="T1">The first type that must also be considered.</typeparam>
/// <typeparam name="T2">The second type that must also be considered.</typeparam>
[AttributeUsage( AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct | AttributeTargets.Enum,
                 AllowMultiple = true,
                 Inherited = false )]
public sealed class AlsoRegisterTypeAttribute<T1, T2> : Attribute
{
}

/// <summary>
/// Enables any selected type to indicate that other types must also be considered.
/// </summary>
/// <typeparam name="T1">The first type that must also be considered.</typeparam>
/// <typeparam name="T2">The second type that must also be considered.</typeparam>
/// <typeparam name="T3">The third type that must also be considered.</typeparam>
[AttributeUsage( AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct | AttributeTargets.Enum,
                 AllowMultiple = true,
                 Inherited = false )]
public sealed class AlsoRegisterTypeAttribute<T1, T2, T3> : Attribute
{
}

/// <summary>
/// Enables any selected type to indicate that other types must also be considered.
/// </summary>
/// <typeparam name="T1">The first type that must also be considered.</typeparam>
/// <typeparam name="T2">The second type that must also be considered.</typeparam>
/// <typeparam name="T3">The third type that must also be considered.</typeparam>
/// <typeparam name="T4">The fourth type that must also be considered.</typeparam>
[AttributeUsage( AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct | AttributeTargets.Enum,
                 AllowMultiple = true,
                 Inherited = false )]
public sealed class AlsoRegisterTypeAttribute<T1, T2, T3, T4> : Attribute
{
}

/// <summary>
/// Enables any selected type to indicate that other types must also be considered.
/// </summary>
/// <typeparam name="T1">The first type that must also be considered.</typeparam>
/// <typeparam name="T2">The second type that must also be considered.</typeparam>
/// <typeparam name="T3">The third type that must also be considered.</typeparam>
/// <typeparam name="T4">The fourth type that must also be considered.</typeparam>
/// <typeparam name="T5">The fifth type that must also be considered.</typeparam>
[AttributeUsage( AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct | AttributeTargets.Enum,
                 AllowMultiple = true,
                 Inherited = false )]
public sealed class AlsoRegisterTypeAttribute<T1, T2, T3, T4, T5> : Attribute
{
}

/// <summary>
/// Enables any selected type to indicate that other types must also be considered.
/// </summary>
/// <typeparam name="T1">The first type that must also be considered.</typeparam>
/// <typeparam name="T2">The second type that must also be considered.</typeparam>
/// <typeparam name="T3">The third type that must also be considered.</typeparam>
/// <typeparam name="T4">The fourth type that must also be considered.</typeparam>
/// <typeparam name="T5">The fifth type that must also be considered.</typeparam>
/// <typeparam name="T6">The sixth type that must also be considered.</typeparam>
[AttributeUsage( AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct | AttributeTargets.Enum,
                 AllowMultiple = true,
                 Inherited = false )]
public sealed class AlsoRegisterTypeAttribute<T1, T2, T3, T4, T5, T6> : Attribute
{
}

/// <summary>
/// Enables any selected type to indicate that other types must also be considered.
/// </summary>
/// <typeparam name="T1">The first type that must also be considered.</typeparam>
/// <typeparam name="T2">The second type that must also be considered.</typeparam>
/// <typeparam name="T3">The third type that must also be considered.</typeparam>
/// <typeparam name="T4">The fourth type that must also be considered.</typeparam>
/// <typeparam name="T5">The fifth type that must also be considered.</typeparam>
/// <typeparam name="T6">The sixth type that must also be considered.</typeparam>
/// <typeparam name="T7">The seventh type that must also be considered.</typeparam>
[AttributeUsage( AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct | AttributeTargets.Enum,
                 AllowMultiple = true,
                 Inherited = false )]
public sealed class AlsoRegisterTypeAttribute<T1, T2, T3, T4, T5, T6, T7> : Attribute
{
}

/// <summary>
/// Enables any selected type to indicate that other types must also be considered.
/// </summary>
/// <typeparam name="T1">The first type that must also be considered.</typeparam>
/// <typeparam name="T2">The second type that must also be considered.</typeparam>
/// <typeparam name="T3">The third type that must also be considered.</typeparam>
/// <typeparam name="T4">The fourth type that must also be considered.</typeparam>
/// <typeparam name="T5">The fifth type that must also be considered.</typeparam>
/// <typeparam name="T6">The sixth type that must also be considered.</typeparam>
/// <typeparam name="T8">The heighth type that must also be considered.</typeparam>
[AttributeUsage( AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct | AttributeTargets.Enum,
                 AllowMultiple = true,
                 Inherited = false )]
public sealed class AlsoRegisterTypeAttribute<T1, T2, T3, T4, T5, T6, T7, T8> : Attribute
{
}
