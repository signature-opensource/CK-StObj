// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage( "Style", "IDE0060:Remove unused parameter", Justification = "This is a Test assembly." )]
[assembly: SuppressMessage( "Naming", "CA1707:Identifiers should not contain underscores", Justification = "In a Test assembly, this is how tests should be named." )]
[assembly: SuppressMessage( "Usage", "CA1801:Review unused parameters", Justification = "This is a Test assembly. We often need to mock methods with no implementation." )]
[assembly: SuppressMessage( "Maintainability", "CA1508:Avoid dead conditional code", Justification = "Too much false positive." )]
[assembly: SuppressMessage( "Design", "CA1062:Validate arguments of public methods", Justification = "Test case methods." )]
