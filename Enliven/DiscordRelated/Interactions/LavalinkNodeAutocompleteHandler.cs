using System;
using System.Linq;
using System.Threading.Tasks;
using Bot.Music.Cluster;
using Bot.Music.Players;
using Discord;
using Discord.Interactions;
using Lavalink4NET.Cluster.Nodes;
using Microsoft.Extensions.DependencyInjection;

namespace Bot.DiscordRelated.Interactions;

public sealed class LavalinkNodeAutocompleteHandler : AutocompleteHandler {
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services) {
        var audioService = services.GetRequiredService<IEnlivenClusterAudioService>();
        var currentNode = audioService.Players.TryGetPlayer<EnlivenLavalinkPlayer>(context.Guild!.Id, out var player)
            ? audioService.GetPlayerNode(player!)
            : null;
        var input = autocompleteInteraction.Data.Current.Value?.ToString() ?? string.Empty;
        var suggestions = audioService.Nodes
            .Where(node => node.Status == LavalinkNodeStatus.Available && node != currentNode)
            .Where(node => node.Label.Contains(input, StringComparison.OrdinalIgnoreCase))
            .Take(25)
            .Select(node => new AutocompleteResult(node.Label, node.Label));
        return Task.FromResult(AutocompletionResult.FromSuccess(suggestions));
    }
}