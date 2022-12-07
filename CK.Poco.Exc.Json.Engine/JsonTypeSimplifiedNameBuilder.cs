using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace CK.Setup.PocoJson
{
    class JsonTypeSimplifiedNameBuilder : ExchangeableTypeNameBuilder
    {
        protected override FullExchangeableTypeName MakeBasicName( IActivityMonitor monitor, IPocoType basic )
        {
            if( basic.Type == typeof( int ) || basic.Type == typeof( uint )
                || basic.Type == typeof( short ) || basic.Type == typeof( ushort )
                || basic.Type == typeof( sbyte ) || basic.Type == typeof( sbyte )
                || basic.Type == typeof( float ) || basic.Type == typeof( double ) )
            {
                return new FullExchangeableTypeName( basic, "Number" );
            }
            if( basic.Type == typeof( long ) || basic.Type == typeof( ulong )
                    || basic.Type == typeof( Decimal ) || basic.Type == typeof( BigInteger ) )
            {
                return new FullExchangeableTypeName( basic, "BigInt" );
            }
            return new FullExchangeableTypeName( basic );
        }
    }
}
