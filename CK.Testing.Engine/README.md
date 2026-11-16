# CK.Testing.Engine

Test helpers that run the CKomposable engine (it was CK.Testing.StObjEngine):
- `CreateDefaultEngineConfiguration`, `GetSuccessfulCollectorResult`, `GetFailedCollectorResult` (extensions of the
  test helper): configure an engine run, check the collection of a set of types;
- `SharedEngine`: one engine run shared by the tests of a test project (`EngineResult`, `Map`, `AutomaticServices`);
- `AutomaticServices` and `ServicedApplication`: a service provider or a started host built from an engine result.

The helpers are in the `CK.Testing` namespace. The testing packages that run the engine are named
`CK.Testing.Engine.*` (CK.Testing.Engine.HttpEndpoint, CK.Testing.Engine.SqlServer, CK.Testing.Engine.TypeScript...).
