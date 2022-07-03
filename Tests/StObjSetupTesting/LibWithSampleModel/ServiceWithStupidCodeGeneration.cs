using CK.Core;
using Sample.Model;

namespace LibWithSampleModel
{
    public abstract class ServiceWithStupidCodeGeneration : IAutoService
    {
        [StupidCode( "\"Hello from generated code!\"", IsLambda = true )]
        public abstract string GetName();
    }
}
