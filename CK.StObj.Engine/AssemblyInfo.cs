
[assembly: CK.Setup.IsSetupDependency()]
// Version 8.0.1--0026-develop introduces IsModelDependent and IsModelDependentSource.
// The new CK.SqlServer.Runtime uses it.
// Unfortunatly, this breaks with -local time based version numbers :(
// I have currently no (clean) solution for this...
// [assembly: CK.Setup.RequiredSetupDependency("CKSetup.Runner", minDependencyVersion: "8.0.1--0026-develop" )]
[assembly: CK.Setup.RequiredSetupDependency("CKSetup.Runner", minDependencyVersion: null )]
