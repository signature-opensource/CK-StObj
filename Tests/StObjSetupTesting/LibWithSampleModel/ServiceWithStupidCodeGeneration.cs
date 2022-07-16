using CK.Core;
using Sample.Model;

namespace LibWithSampleModel
{
    public abstract class ServiceWithStupidCodeGeneration : IAutoService
    {
        [StupidCode( "\"Hello from generated code! (touch)\"", IsLambda = true )]
        public abstract string GetName();
    }
}
