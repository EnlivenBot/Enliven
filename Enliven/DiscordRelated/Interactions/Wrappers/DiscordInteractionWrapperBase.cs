using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;

// ReSharper disable InconsistentNaming

namespace Bot.DiscordRelated.Interactions.Wrappers;

public class DiscordInteractionWrapperBase(IDiscordInteraction interaction) : IEnlivenInteraction {
    private bool _isRespondStarted;

    protected IDiscordInteraction Interaction { get; } = interaction;

    public bool NeedResponse => !HasResponded;

    public bool HasResponded => _isRespondStarted || Interaction.HasResponded;

    public bool CurrentResponseDeferred { get; protected set; }

    public bool CurrentResponseMeaningful { get; protected set; }
    
    protected void SetRespondStarted() {
        _isRespondStarted = true;
    }

    public async Task RespondAsync(string? text = null, Embed[]? embeds = null, bool isTTS = false,
        bool ephemeral = false,
        AllowedMentions? allowedMentions = null, MessageComponent? components = null, Embed? embed = null,
        RequestOptions? options = null, PollProperties? poll = null) {
        SetRespondStarted();
        CurrentResponseDeferred = false;
        CurrentResponseMeaningful = true;
        await Interaction.RespondAsync(text, embeds, isTTS, ephemeral, allowedMentions, components, embed, options,
            poll);
    }

    public async Task RespondWithFilesAsync(IEnumerable<FileAttachment> attachments, string? text = null,
        Embed[]? embeds = null, bool isTTS = false,
        bool ephemeral = false, AllowedMentions? allowedMentions = null, MessageComponent? components = null,
        Embed? embed = null, RequestOptions? options = null, PollProperties? poll = null) {
        SetRespondStarted();
        CurrentResponseDeferred = false;
        CurrentResponseMeaningful = true;
        await Interaction.RespondWithFilesAsync(attachments, text, embeds, isTTS, ephemeral, allowedMentions,
            components, embed, options, poll);
    }

    public Task<IUserMessage> FollowupAsync(string? text = null, Embed[]? embeds = null, bool isTTS = false,
        bool ephemeral = false,
        AllowedMentions? allowedMentions = null, MessageComponent? components = null, Embed? embed = null,
        RequestOptions? options = null, PollProperties? poll = null) {
        SetRespondStarted();
        return Interaction.FollowupAsync(text, embeds, isTTS, ephemeral, allowedMentions, components, embed, options,
            poll);
    }

    public Task<IUserMessage> FollowupWithFilesAsync(IEnumerable<FileAttachment> attachments, string? text = null,
        Embed[]? embeds = null, bool isTTS = false,
        bool ephemeral = false, AllowedMentions? allowedMentions = null, MessageComponent? components = null,
        Embed? embed = null, RequestOptions? options = null, PollProperties? poll = null) {
        SetRespondStarted();
        return Interaction.FollowupWithFilesAsync(attachments, text, embeds, isTTS, ephemeral, allowedMentions,
            components, embed, options, poll);
    }

    public Task<IUserMessage> GetOriginalResponseAsync(RequestOptions? options = null) {
        return Interaction.GetOriginalResponseAsync(options);
    }

    public Task<IUserMessage> ModifyOriginalResponseAsync(Action<MessageProperties> func,
        RequestOptions? options = null) {
        return Interaction.ModifyOriginalResponseAsync(func, options);
    }

    public async Task DeleteOriginalResponseAsync(RequestOptions? options = null) {
        CurrentResponseMeaningful = false;
        CurrentResponseDeferred = false;
        await Interaction.DeleteOriginalResponseAsync(options);
    }

    public async Task DeferAsync(bool ephemeral = false, RequestOptions? options = null) {
        SetRespondStarted();
        CurrentResponseDeferred = true;
        await Interaction.DeferAsync(ephemeral, options);
    }

    public Task RespondWithModalAsync(Modal modal, RequestOptions? options = null) {
        return Interaction.RespondWithModalAsync(modal, options);
    }

    public Task RespondWithPremiumRequiredAsync(RequestOptions? options = null) {
        return Interaction.RespondWithPremiumRequiredAsync(options);
    }

    public ulong Id => Interaction.Id;

    public InteractionType Type => Interaction.Type;

    public IDiscordInteractionData Data => Interaction.Data;

    public string Token => Interaction.Token;

    public int Version => Interaction.Version;

    public IUser User => Interaction.User;

    public string UserLocale => Interaction.UserLocale;

    public string GuildLocale => Interaction.GuildLocale;

    public bool IsDMInteraction => Interaction.IsDMInteraction;

    public ulong? ChannelId => Interaction.ChannelId;

    public ulong? GuildId => Interaction.GuildId;

    public ulong ApplicationId => Interaction.ApplicationId;

    public IReadOnlyCollection<IEntitlement> Entitlements => Interaction.Entitlements;

    public IReadOnlyDictionary<ApplicationIntegrationType, ulong> IntegrationOwners => Interaction.IntegrationOwners;

    public InteractionContextType? ContextType => Interaction.ContextType;

    public GuildPermissions Permissions => Interaction.Permissions;

    public DateTimeOffset CreatedAt => Interaction.CreatedAt;
}