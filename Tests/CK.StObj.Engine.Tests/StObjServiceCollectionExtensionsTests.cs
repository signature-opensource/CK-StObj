using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace CK.StObj.Engine.Tests;

public class StObjServiceCollectionExtensionsTests
{
    interface IA { }
    interface IB : IA { }
    class A : IB, IA { }

    [TestCase( true )]
    [TestCase( false )]
    public void Microsoft_conformant_DI_does_not_resolve_mappings_to_the_same_instance_and_impl_is_not_mapped( bool scoped )
    {
        IServiceCollection services = new ServiceCollection();
        services.Add( new ServiceDescriptor( typeof( IA ), typeof( A ), scoped ? ServiceLifetime.Scoped : ServiceLifetime.Singleton ) );
        services.Add( new ServiceDescriptor( typeof( IB ), typeof( A ), scoped ? ServiceLifetime.Scoped : ServiceLifetime.Singleton ) );

        var sp = services.BuildServiceProvider();

        sp.GetService( typeof( A ) ).ShouldBeNull();
        var a = sp.GetRequiredService<IA>();
        var b = sp.GetRequiredService<IB>();
        a.ShouldNotBeSameAs( b );
    }

}
