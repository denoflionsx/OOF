using Dalamud.Game.ClientState.Objects;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Game;
using Dalamud.Plugin.Services;

namespace OofPlugin;

public class Service
{
    [PluginService] public static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] public static IPluginLog Logger { get; private set; } = null!;

    public static void Initialize(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();
    }
}
