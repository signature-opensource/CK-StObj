using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace CK.Setup
{
    static class ILGeneratorExtension
    {
        /// <summary>
        /// Emits the optimal IL to push the actual parameter values on the stack (<see cref="OpCodes.Ldarg_0"/>... <see cref="OpCodes.Ldarg"/>).
        /// </summary>
        /// <param name="g">This <see cref="ILGenerator"/> object.</param>
        /// <param name="startAtArgument0">False to skip the very first argument: for a method instance Arg0 is the 'this' object (see <see cref="System.Reflection.CallingConventions"/>) HasThis and ExplicitThis).</param>
        /// <param name="count">Number of parameters to push.</param>
        public static void RepushActualParameters( this ILGenerator g, bool startAtArgument0, int count )
        {
            if( count <= 0 ) return;
            if( startAtArgument0 )
            {
                g.Emit( OpCodes.Ldarg_0 );
                --count;
            }
            if( count > 0 )
            {
                g.Emit( OpCodes.Ldarg_1 );
                if( count > 1 )
                {
                    g.Emit( OpCodes.Ldarg_2 );
                    if( count > 2 )
                    {
                        g.Emit( OpCodes.Ldarg_3 );
                        if( count > 3 )
                        {
                            for( int iParam = 4; iParam <= Math.Min( count, 255 ); ++iParam )
                            {
                                g.Emit( OpCodes.Ldarg_S, (byte)iParam );
                            }
                            if( count > 255 )
                            {
                                for( int iParam = 256; iParam <= count; ++iParam )
                                {
                                    g.Emit( OpCodes.Ldarg, (short)iParam );
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Emits the IL to push (<see cref="OpCodes.Ldarg"/>) the actual argument at the given index onto the stack.
        /// </summary>
        /// <param name="g">This <see cref="ILGenerator"/> object.</param>
        /// <param name="i">Parameter index (0 being the 'this' for instance method).</param>
        public static void LdArg( this ILGenerator g, int i )
        {
            if( i == 0 ) g.Emit( OpCodes.Ldarg_0 );
            else if( i == 1 ) g.Emit( OpCodes.Ldarg_1 );
            else if( i == 2 ) g.Emit( OpCodes.Ldarg_2 );
            else if( i == 3 ) g.Emit( OpCodes.Ldarg_3 );
            else if( i < 255 ) g.Emit( OpCodes.Ldarg_S, (byte)i );
            else g.Emit( OpCodes.Ldarg, (short)i );
        }

        /// <summary>
        /// Emits the IL to push (<see cref="OpCodes.Ldloc"/>) the given local on top of the stack.
        /// </summary>
        /// <param name="g">This <see cref="ILGenerator"/> object.</param>
        /// <param name="local">The local variable to push.</param>
        public static void LdLoc( this ILGenerator g, LocalBuilder local )
        {
            int i = local.LocalIndex;
            if( i == 0 ) g.Emit( OpCodes.Ldloc_0 );
            else if( i == 1 ) g.Emit( OpCodes.Ldloc_1 );
            else if( i == 2 ) g.Emit( OpCodes.Ldloc_2 );
            else if( i == 3 ) g.Emit( OpCodes.Ldloc_3 );
            else if( i < 255 ) g.Emit( OpCodes.Ldloc_S, (byte)i );
            else g.Emit( OpCodes.Ldloc, (short)i );
        }

        /// <summary>
        /// Emits code that sets the parameter (that must be a 'ref' or 'out' parameter) to the default of its type.
        /// Handles static or instance methods and value or reference type.
        /// </summary>
        /// <param name="g">This <see cref="ILGenerator"/> object.</param>
        /// <param name="byRefParameter">The 'by ref' parameter.</param>
        public static void StoreDefaultValueForOutParameter( this ILGenerator g, ParameterInfo byRefParameter )
        {
            Throw.CheckArgument( byRefParameter.ParameterType.IsByRef );
            Type pType = byRefParameter.ParameterType.GetElementType()!;
            // Adds 1 to skip 'this' parameter ?
            MethodBase m = (MethodBase)byRefParameter.Member;
            if( (m.CallingConvention & CallingConventions.HasThis) != 0 ) g.LdArg( byRefParameter.Position + 1 );
            else g.LdArg( byRefParameter.Position );
            if( pType.GetTypeInfo().IsValueType )
            {
                g.Emit( OpCodes.Initobj, pType );
            }
            else
            {
                g.Emit( OpCodes.Ldnull );
                g.Emit( OpCodes.Stind_Ref );
            }
        }

    }
}
