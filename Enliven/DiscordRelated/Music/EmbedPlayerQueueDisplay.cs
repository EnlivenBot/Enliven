﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Bot.DiscordRelated.Interactions.Handlers;
using Bot.DiscordRelated.MessageComponents;
using Bot.Utilities.Collector;
using Common;
using Common.Localization.Entries;
using Common.Localization.Providers;
using Common.Music.Players;
using Common.Utils;
using Discord;

namespace Bot.DiscordRelated.Music;

public class EmbedPlayerQueueDisplay : PlayerDisplayBase
{
    private readonly CollectorService _collectorService;
    private readonly IDiscordClient _discordClient;
    private readonly ILocalizationProvider _loc;
    private readonly MessageComponentInteractionsHandler _messageComponentInteractionsHandler;
    private readonly IMessageChannel _targetChannel;

    private PaginatedMessage _paginatedMessage = null!;
    private Disposables? _subscribers;

    public EmbedPlayerQueueDisplay(IMessageChannel targetChannel, ILocalizationProvider loc,
        MessageComponentInteractionsHandler messageComponentInteractionsHandler, CollectorService collectorService,
        IDiscordClient discordClient)
    {
        _loc = loc;
        _messageComponentInteractionsHandler = messageComponentInteractionsHandler;
        _collectorService = collectorService;
        _discordClient = discordClient;
        _targetChannel = targetChannel;
    }

    public override async Task Initialize(EnlivenLavalinkPlayer finalLavalinkPlayer)
    {
        var paginatedAppearanceOptions = new PaginatedAppearanceOptions { Timeout = TimeSpan.FromMinutes(1) };
        _paginatedMessage = new PaginatedMessage(paginatedAppearanceOptions, _targetChannel, _loc,
            _messageComponentInteractionsHandler, _collectorService, _discordClient)
        {
            Title = _loc.Get("MusicQueues.QueueTitle"), Color = Color.Gold
        };
        _ = _paginatedMessage.WaitForDisposeAsync().ContinueWith(_ => ExecuteShutdown(null, null));
        await base.Initialize(finalLavalinkPlayer);
        await _paginatedMessage.Resend();
    }

    private void UpdatePages()
    {
        _paginatedMessage?.SetPages(string.Join("\n",
                Player!.Playlist.Select((track, i) =>
                    (Player.CurrentTrackIndex == i ? "@" : " ")
                    + $"{i + 1}: {Player.Playlist[i].Track.Title.RemoveNonPrintableChars()}")),
            "```py\n{0}```", 50);
    }


    public override Task ChangePlayer(EnlivenLavalinkPlayer newPlayer)
    {
        base.ChangePlayer(newPlayer);
        UpdatePages();
        _subscribers?.Dispose();
        _subscribers = new Disposables(
            newPlayer.Playlist.Changed.Subscribe(_ => UpdatePages()),
            newPlayer.CurrentTrackIndexChanged.Subscribe(_ => UpdatePages())
        );
        return Task.CompletedTask;
    }

    public override async Task ExecuteShutdown(IEntry? header, IEntry? body)
    {
        await base.ExecuteShutdown(header!, body!);
        _subscribers?.Dispose();
        _paginatedMessage.Dispose();
    }

    public override Task LeaveNotification(IEntry? header, IEntry? body)
    {
        return Task.CompletedTask;
    }
}