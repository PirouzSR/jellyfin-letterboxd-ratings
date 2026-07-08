using Jellyfin.Plugin.Letterboxd.Letterboxd;
using Jellyfin.Plugin.Letterboxd.Web;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Letterboxd;

/// <summary>
/// Registers plugin services with the server's dependency injection container.
/// </summary>
public class ServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<LetterboxdClient>();
        serviceCollection.AddSingleton<RatingCache>();
        serviceCollection.AddHostedService<WebInterfaceInjector>();
    }
}
