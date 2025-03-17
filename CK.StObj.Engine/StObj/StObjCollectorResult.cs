using CK.Core;

#nullable enable

namespace CK.Setup;

/// <summary>
/// Encapsulates the result of the <see cref="StObjCollector"/> work.
/// </summary>
public partial class StObjCollectorResult
{
    readonly DynamicAssembly _tempAssembly;
    readonly BuildValueCollector? _valueCollector;
    readonly DIContainerAnalysisResult? _endpointResult;
    readonly CKTypeCollectorResult _typeResult;
    readonly IStObjEngineMap? _engineMap;

    internal StObjCollectorResult( CKTypeCollectorResult typeResult,
                                   DynamicAssembly tempAssembly,
                                   DIContainerAnalysisResult? e,
                                   BuildValueCollector? valueCollector )
    {
        Throw.DebugAssert( "typeResult.HasFatalError ==> valueCollector == null (i.e. valueCollector != null ==> !typeResult.HasFatalError)",
                           !typeResult.HasFatalError || valueCollector == null );
        _typeResult = typeResult;
        _tempAssembly = tempAssembly;
        _valueCollector = valueCollector;
        _endpointResult = e;
        if( valueCollector != null )
        {
            _engineMap = typeResult.RealObjects.EngineMap;
        }
    }

    /// <summary>
    /// True if a fatal error occurred. Result should be discarded.
    /// </summary>
    public bool HasFatalError => _valueCollector == null || _endpointResult == null;

    /// <summary>
    /// Gets the result of the types discovery and analysis.
    /// </summary>
    public CKTypeCollectorResult CKTypeResult => _typeResult;

    /// <summary>
    /// Gets the Poco Type System builder.
    /// </summary>
    public IPocoTypeSystemBuilder PocoTypeSystemBuilder => _typeResult.PocoTypeSystemBuilder;

    /// <summary>
    /// Gets the endpoint results if no error occurred during analysis, null otherwise.
    /// </summary>
    public DIContainerAnalysisResult? EndpointResult => _endpointResult;

    /// <summary>
    /// Gets the final <see cref="IStObjEngineMap"/> if <see cref="HasFatalError"/> is false.
    /// </summary>
    public IStObjEngineMap? EngineMap => _engineMap;

    /// <summary>
    /// Gets the dynamic assembly for this context.
    /// </summary>
    public IDynamicAssembly DynamicAssembly => _tempAssembly;
}
