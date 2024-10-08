﻿using System.Threading.Tasks;
using Common;
using Common.Config;
using Discord;
using Discord.WebSocket;

namespace Bot;

public class GlobalBehaviorsService : IService {
    private readonly EnlivenShardedClient _discordClient;
    private IGuildConfigProvider _guildConfigProvider;

    public GlobalBehaviorsService(IGuildConfigProvider guildConfigProvider, EnlivenShardedClient discordClient) {
        _guildConfigProvider = guildConfigProvider;
        _discordClient = discordClient;
    }

    public Task OnPostDiscordStart() {
        _discordClient.JoinedGuild += ClientOnJoinedGuild;
        return Task.CompletedTask;
    }

    private async Task ClientOnJoinedGuild(SocketGuild arg) {
        _guildConfigProvider.Get(arg.Id);
        await PrintWelcomeMessage(arg);
    }

    public async Task<IUserMessage> PrintWelcomeMessage(SocketGuild guild, IMessageChannel? channel = null) {
        var guildConfig = _guildConfigProvider.Get(guild.Id);
        var loc = guildConfig.Loc;

        var embedBuilder = new EmbedBuilder()
            .WithColor(Color.Gold)
            .WithFooter($"Powered by {_discordClient.CurrentUser.Username}")
            .WithAuthor(_discordClient.CurrentUser.Username, _discordClient.CurrentUser.GetAvatarUrl())
            .WithDescription(loc.Get("Messages.WelcomeDescription").Format(guildConfig.Prefix, _discordClient.CurrentUser.Mention))
            .AddField(loc.Get("Messages.WelcomeMusicTitle"), loc.Get("Messages.WelcomeMusic"))
            .AddField(loc.Get("Messages.WelcomeLoggingTitle"), loc.Get("Messages.WelcomeLogging"))
            .AddField(loc.Get("Messages.WelcomeLocalizationTitle"), loc.Get("Messages.WelcomeLocalization"))
            .AddField(loc.Get("Messages.WelcomeInfoTitle"), loc.Get("Messages.WelcomeInfo"))
            .AddField(loc.Get("Messages.WelcomeGithubTitle"), loc.Get("Messages.WelcomeGithub"));
        return await (channel ?? guild.DefaultChannel).SendMessageAsync(null, false, embedBuilder.Build());
    }
}