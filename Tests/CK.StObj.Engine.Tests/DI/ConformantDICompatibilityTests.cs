using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace CK.StObj.Engine.Tests.DI
{
    [TestFixture]
    public class ConformantDICompatibilityTests
    {
        [IsMultiple]
        public interface IEndpointServiceResolver : ISingletonAutoService
        {
            object? GetService( IServiceProvider scope, Type serviceType );
        }

        public sealed class ServiceHook : IScopedAutoService
        {
            readonly IServiceProvider _scoped;
            IEndpointServiceResolver? _resolver;
            readonly Dictionary<Type, object?> _resolved;

            public ServiceHook( IServiceProvider scoped )
            {
                _resolved = new Dictionary<Type, object?>();
                _scoped = scoped;
            }

            public void SetResolver( IEndpointServiceResolver resolver )
            {
                Throw.CheckState( _resolver == null );
                _resolver = resolver;
            }

            public object? GetService( Type type )
            {
                Throw.CheckState( _resolver != null );
                if( _resolved.TryGetValue( type, out object? value ) ) return value;
                var o = _resolver.GetService( _scoped, type );
                _resolved.Add( type, o );
                return o;
            }
        }

        public sealed class CommonSingleton { }

        public sealed class CommonScoped { }

        /// <summary>
        /// Web specific singleton service: the same instance must be available to all Web end points.
        /// </summary>
        public sealed class HttpContextAccessor { }

        /// <summary>
        /// Web specific scoped service.
        /// </summary>
        public sealed class HttpRequest { }

        /// <summary>
        /// MQTT specific singleton service: the same instance must be available to all MQTT end points.
        /// </summary>
        public sealed class MQTTServer { }

        /// <summary>
        /// MQTT specific scoped service.
        /// </summary>
        public sealed class MQTTCaller { }


        public sealed class WebEndpointServices : IEndpointServiceResolver
        {
            readonly IServiceProvider _primary;

            public WebEndpointServices( IServiceProvider primary )
            {
                _primary = primary;
            }

            public object? GetService( IServiceProvider scope, Type serviceType )
            {
                // The fact that the EndpointServiceResolver knows what it cannot handle is
                // bearable.
                if( serviceType == typeof( MQTTServer )
                    || serviceType == typeof( MQTTCaller ) )
                {
                    return null;
                }
                // This is annoying: we must know the singleton/scoped lifetime.
                if( serviceType == typeof( HttpContextAccessor ) )
                {
                    return _primary.GetService( serviceType );
                }
                // Here we are annoyed... to say the least.
                // To call this we must be sure that the hook will not kick-in again...
                // ...and again...and...
                return scope.GetService( serviceType );
            }
        }

        public sealed class MQTTEndpointServices : IEndpointServiceResolver
        {
            readonly IServiceProvider _primary;

            public MQTTEndpointServices( IServiceProvider primary )
            {
                _primary = primary;
            }


            public object? GetService( IServiceProvider scope, Type serviceType )
            {
                if( serviceType == typeof( HttpContextAccessor )
                    || serviceType == typeof( HttpRequest ) )
                {
                    return null;
                }
                if( serviceType == typeof( MQTTServer ) )
                {
                    return _primary.GetService( serviceType );
                }
                return scope.GetService( serviceType );
            }
        }

        [Explicit("This hook cannot work. This is kept here for memory.")]
        public void Scope_and_resolution_hook()
        {
            var builder = new ServiceCollection();
            builder.AddScoped<ServiceHook>();

            builder.AddSingleton<MQTTEndpointServices>();
            builder.AddSingleton<IEndpointServiceResolver>( sp => sp.GetRequiredService<MQTTEndpointServices>() );
            builder.AddSingleton<WebEndpointServices>();
            builder.AddSingleton<IEndpointServiceResolver>( sp => sp.GetRequiredService<WebEndpointServices>() );

            builder.AddSingleton<CommonSingleton>();
            builder.AddScoped<CommonScoped>();

            // We cannot register the singletons as singletons because of an optimization that ignores the scope
            // when ServiceDescriptor.Lifetime == ServiceLifetime.Singleton.
            // But this creates a ServiceHook in the primary container during the second call.
            // There is no way to make this hook work. We need to only think in terms of IServiceCollection configuration
            // and rely on a simple, definitely built, service provider.
            builder.AddScoped( typeof( MQTTServer ), sp => sp.GetRequiredService<ServiceHook>().GetService( typeof(MQTTServer) ) );
            builder.AddScoped( typeof( MQTTCaller ) );

            builder.AddScoped( typeof( HttpContextAccessor ), sp => sp.GetRequiredService<ServiceHook>().GetService( typeof( HttpContextAccessor ) ) );
            builder.AddScoped( typeof( HttpRequest ) );

            var primary = builder.BuildServiceProvider();
            var webScope = primary.CreateAsyncScope();
            var hW = webScope.ServiceProvider.GetRequiredService<ServiceHook>();
            hW.SetResolver( primary.GetRequiredService<WebEndpointServices>() );

            //var common = webScope.ServiceProvider.GetRequiredService<CommonSingleton>();
            //webScope.ServiceProvider.GetService<HttpContextAccessor>().Should().NotBeNull();
            //webScope.ServiceProvider.GetService<HttpRequest>().Should().NotBeNull();
            //webScope.ServiceProvider.GetService<MQTTServer>().Should().BeNull();
            //webScope.ServiceProvider.GetService<MQTTCaller>().Should().BeNull();


            //var mqttScope = primary.CreateAsyncScope();
            //var hM = mqttScope.ServiceProvider.GetRequiredService<ServiceHook>();
            //hM.SetResolver( primary.GetRequiredService<MQTTEndpointServices>() );

            //mqttScope.ServiceProvider.GetRequiredService<CommonSingleton>().Should().BeSameAs( common );
            //mqttScope.ServiceProvider.GetService<HttpContextAccessor>().Should().BeNull();
            //mqttScope.ServiceProvider.GetService<HttpRequest>().Should().BeNull();
            //mqttScope.ServiceProvider.GetService<MQTTServer>().Should().NotBeNull();
            //mqttScope.ServiceProvider.GetService<MQTTCaller>().Should().NotBeNull();

        }
    }
}
