using CK.CodeGen;
using CK.Core;
using CK.Setup;
using Sample.Model;
using System.Diagnostics;
using System.Reflection;

namespace Sample.Engine
{
    public class StupidCodeAttributeImpl : IAutoImplementorMethod
    {
        readonly StupidCodeAttribute _attr;

        /// <summary>
        /// The "attribute implementation" is provided with the original, ("Model" only) attribute: any configuration
        /// can be used.
        /// </summary>
        /// <param name="attr">The model layer attribute.</param>
        public StupidCodeAttributeImpl( StupidCodeAttribute attr )
        {
            _attr = attr;
        }

        public CSCodeGenerationResult Implement( IActivityMonitor monitor,
                                                 MethodInfo m,
                                                 ICSCodeGenerationContext codeGenContext,
                                                 ITypeScope typeBuilder )
        {
            IFunctionScope mB = typeBuilder.CreateOverride( m );
            Debug.Assert( mB.Parent == typeBuilder, "The function is ready to be implemented." );
            if( _attr.IsLambda )
            {
                mB.Append( "=> " ).Append( _attr.ActualCode ).Append( ';' ).NewLine();
            }
            else
            {
                mB.Append( _attr.ActualCode ).NewLine();
            }
            return CSCodeGenerationResult.Success;
        }
    }
}
