# Json Exchange Code Generator

This engine package generates code to support export and import of Poco types in Json format.

The work starts with the [CommonImpl](CommonImpl.cs) code generator that is triggered by the
[PocoJsonExportSupport](../CK.Poco.Exc.Json/Export/PocoJsonExportSupport.cs) model package:
```csharp
[ContextBoundDelegation( "CK.Setup.PocoJson.CommonImpl, CK.Poco.Exc.Json.Engine" )]
public static class PocoJsonExportSupport
{
  // ...
}
```
This common code generator then:
- Registers itself as the `IPocoJsonService` so that other engines can know the Json names if they need them. 
- Waits for the PocoTypeSystem to be locked (this waits until no new types have  been registered into 
  the [IPocoTypeSystem](../CK.StObj.Runtime/Poco/PocoTypeSystem/IPocoTypeSystem.cs) by other code generators
- Builds the names that will be used for types (thanks to the [ExchangeableTypeNameBuilder](../CK.Poco.Exchange.Engine/ExchangeableTypeNameBuilder.cs) 
  in CK.Poco.Exchange.Engine).
- Creates the `CK.Poco.Exc.JsonGen` namespace and the 2 internal generated static classes `Exporter` and `Importer` 
  in it that will implement the different methods.
- Delegates the actual code generation to the [ExportCodeGenerator](Export/README.md) and [ImportCodeGenerator](Import/README.md).

