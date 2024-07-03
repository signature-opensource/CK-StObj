using CK.CodeGen;
using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class ExportCodeWriter
    {
        readonly object? _key;
        internal readonly PocoTypeRawSet? _handledTypes;
        internal ExportCodeWriter? _prev;
        internal int _index;

        /// <summary>
        /// Initializes a new code writer, optionally tracking types that use it.
        /// </summary>
        /// <param name="map">The map to which this writer belongs.</param>
        /// <param name="handleTypes">True to track the types in the <see cref="HandledTypes"/>.</param>
        protected ExportCodeWriter( ExportCodeWriterMap map, bool handleTypes = false )
        {
            _handledTypes = handleTypes ? new PocoTypeRawSet( map.NameMap.TypeSystem ) : null;
        }

        /// <summary>
        /// Initializes a new keyed code writer, optionally tracking types that use it.
        /// </summary>
        /// <param name="map">The map to which this writer belongs.</param>
        /// <param name="key">The key that identifies this writer.</param>
        /// <param name="handleTypes">True to track the types in the <see cref="HandledTypes"/>.</param>
        protected ExportCodeWriter( ExportCodeWriterMap map, object key, bool handleTypes = false )
            : this( map, handleTypes ) 
        {
            Throw.CheckNotNullArgument( key );
            _key = key;
        }

        /// <summary>
        /// Gets a unique incremented index that identifies this writer.
        /// This is -1 for the "any writer" created by <see cref="ExportCodeWriterMap.CreateAnyWriter"/>, other writers
        /// index is 0 or positive.
        /// </summary>
        public int Index => _index;

        /// <summary>
        /// Gets the optional key that identifies this writer.
        /// </summary>
        public object? Key => _key;

        /// <summary>
        /// Generates the code to write a nulll value.
        /// </summary>
        /// <param name="writer">The target code.</param>
        public abstract void WriteNull( ICodeWriter writer );

        /// <summary>
        /// Gets the set of types handled by this writer if tracking type has been enabled, null otherwise.
        /// </summary>
        public IReadOnlyPocoTypeSet? HandledTypes => _handledTypes;

        /// <summary>
        /// Generates the write code of a non null <paramref name="variableName"/>.
        /// </summary>
        /// <param name="writer">The code writer to uses.</param>
        /// <param name="variableName">The variable name to write.</param>
        public abstract void RawWrite( ICodeWriter writer, string variableName );

        /// <summary>
        /// Generates the write code of a <paramref name="variableName"/> that can be null or not.
        /// <para>
        /// The <paramref name="type"/> must be compatible with this writer.
        /// </para>
        /// </summary>
        /// <param name="writer">The code writer to uses.</param>
        /// <param name="type">The type of the variable to write.</param>
        /// <param name="variableName">The variable name to write.</param>
        public virtual void GenerateWrite( ICodeWriter writer, IPocoType type, string variableName )
        {
            GenerateWrite( writer, variableName, type.Type.IsValueType, type.IsNullable, type is IRecordPocoType );
        }

        /// <summary>
        /// Generates the write code of a <paramref name="variableName"/>.
        /// </summary>
        /// <param name="writer">The code writer to uses.</param>
        /// <param name="variableName">The variable name to write.</param>
        /// <param name="isValueType">True the type is a value type.</param>
        /// <param name="isNullable">True if the type is nullable.</param>
        /// <param name="isPocoRecordType">True if the type is a record type: record type are passed by reference.</param>
        public virtual void GenerateWrite( ICodeWriter writer, string variableName, bool isValueType, bool isNullable, bool isPocoRecordType )
        {
            if( isValueType )
            {
                if( isNullable )
                {
                    writer.Append( "if( !" ).Append( variableName ).Append( ".HasValue ) " );
                    WriteNull( writer );
                    writer.NewLine()
                          .Append( "else" )
                          .OpenBlock();
                    if( isPocoRecordType )
                    {
                        variableName = $"CommunityToolkit.HighPerformance.NullableExtensions.DangerousGetValueOrDefaultReference(ref {variableName})";
                    }
                    else
                    {
                        variableName = $"{variableName}.Value";
                    }
                    RawWrite( writer, variableName );
                    writer.CloseBlock();
                }
                else
                {
                    RawWrite( writer, variableName );
                    writer.NewLine();
                }
            }
            else
            {
                if( isNullable )
                {
                    writer.Append( "if( " ).Append( variableName ).Append( " == null ) " );
                    WriteNull( writer );
                    writer.NewLine()
                          .Append( "else" )
                          .OpenBlock();
                }
                RawWrite( writer, variableName );
                if( isNullable ) writer.CloseBlock();
            }
        }

        /// <summary>
        /// Generates any required support code required by <see cref="RawWrite(ICodeWriter, string)"/>.
        /// <para>
        /// Does nothing by default: not all writer require support code.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <param name="generationContext">The code generation context.</param>
        /// <param name="writers">The writers map.</param>
        /// <param name="exporterType">The generated exporter type code.</param>
        /// <param name="pocoDirectoryType">The generated PocoDirectory type code.</param>
        internal protected virtual void GenerateSupportCode( IActivityMonitor monitor,
                                                             ICSCodeGenerationContext generationContext,
                                                             ExportCodeWriterMap writers,
                                                             ITypeScope exporterType,
                                                             ITypeScope pocoDirectoryType )
        {
        }
    }
}
