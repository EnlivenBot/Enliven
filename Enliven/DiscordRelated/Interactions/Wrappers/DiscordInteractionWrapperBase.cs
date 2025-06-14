using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;

// ReSharper disable InconsistentNaming

namespace Bot.DiscordRelated.Interactions.Wrappers;

public class DiscordInteractionWrapperBase(IDiscordInteraction interaction) : IEnlivenInteraction
{
    protected IDiscordInteraction Interaction { get; } = interaction;

    public bool NeedResponse => !Interaction.HasResponded;

    public bool CurrentResponseDeferred { get; protected set; }

    public bool CurrentResponseMeaningful { get; protected set; }

    public async Task RespondAsync(string? text = null, Embed[]? embeds = null, bool isTTS = false,
        bool ephemeral = false,
        AllowedMentions? allowedMentions = null, MessageComponent? components = null, Embed? embed = null,
        RequestOptions? options = null, PollProperties? poll = null)
    {
        await Interaction.RespondAsync(text, embeds, isTTS, ephemeral, allowedMentions, components, embed, options,
            poll);
        CurrentResponseDeferred = false;
        CurrentResponseMeaningful = true;
    }

    public async Task RespondWithFilesAsync(IEnumerable<FileAttachment> attachments, string? text = null,
        Embed[]? embeds = null, bool isTTS = false,
        bool ephemeral = false, AllowedMentions? allowedMentions = null, MessageComponent? components = null,
        Embed? embed = null, RequestOptions? options = null, PollProperties? poll = null)
    {
        await Interaction.RespondWithFilesAsync(attachments, text, embeds, isTTS, ephemeral, allowedMentions,
            components, embed, options, poll);
        CurrentResponseDeferred = false;
        CurrentResponseMeaningful = true;
    }

    public Task<IUserMessage> FollowupAsync(string? text = null, Embed[]? embeds = null, bool isTTS = false,
        bool ephemeral = false,
        AllowedMentions? allowedMentions = null, MessageComponent? components = null, Embed? embed = null,
        RequestOptions? options = null, PollProperties? poll = null)
    {
        return Interaction.FollowupAsync(text, embeds, isTTS, ephemeral, allowedMentions, components, embed, options,
            poll);
    }

    public Task<IUserMessage> FollowupWithFilesAsync(IEnumerable<FileAttachment> attachments, string? text = null,
        Embed[]? embeds = null, bool isTTS = false,
        bool ephemeral = false, AllowedMentions? allowedMentions = null, MessageComponent? components = null,
        Embed? embed = null, RequestOptions? options = null, PollProperties? poll = null)
    {
        return Interaction.FollowupWithFilesAsync(attachments, text, embeds, isTTS, ephemeral, allowedMentions,
            components, embed, options, poll);
    }

    public Task<IUserMessage> GetOriginalResponseAsync(RequestOptions? options = null)
    {
        return Interaction.GetOriginalResponseAsync(options);
    }

    public Task<IUserMessage> ModifyOriginalResponseAsync(Action<MessageProperties> func,
        RequestOptions? options = null)
    {
        return Interaction.ModifyOriginalResponseAsync(func, options);
    }

    public async Task DeleteOriginalResponseAsync(RequestOptions? options = null)
    {
        await Interaction.DeleteOriginalResponseAsync(options);
        CurrentResponseMeaningful = false;
        CurrentResponseDeferred = false;
    }

    public async Task DeferAsync(bool ephemeral = false, RequestOptions? options = null)
    {
        await Interaction.DeferAsync(ephemeral, options);
        CurrentResponseDeferred = true;
    }

    public Task RespondWithModalAsync(Modal modal, RequestOptions? options = null)
    {
        return Interaction.RespondWithModalAsync(modal, options);
    }

    public Task RespondWithPremiumRequiredAsync(RequestOptions? options = null)
    {
        return Interaction.RespondWithPremiumRequiredAsync(options);
    }

    public ulong Id => Interaction.Id;

    public InteractionType Type => Interaction.Type;

    public IDiscordInteractionData Data => Interaction.Data;

    public string Token => Interaction.Token;

    public int Version => Interaction.Version;

    public bool HasResponded => Interaction.HasResponded;

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