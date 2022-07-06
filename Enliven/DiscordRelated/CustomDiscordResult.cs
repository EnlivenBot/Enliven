using System;
using Discord.Commands;

namespace Bot.DiscordRelated {
    public class CustomDiscordResult : IResult {
        public static CustomDiscordResult FromSuccess() {
            return new CustomDiscordResult();
        }

        public static CustomDiscordResult FromError(CommandError error, string reason) {
            return new CustomDiscordResult(error, reason);
        }

        public static CustomDiscordResult FromError(Exception ex) {
            return CustomDiscordResult.FromError(CommandError.Exception, ex.Message);
        }

        public static CustomDiscordResult FromError(IResult result) {
            return new CustomDiscordResult(result.Error, result.ErrorReason);
        }

        private CustomDiscordResult(CommandError? error, string errorReason) {
            Error = error;
            ErrorReason = errorReason;
        }

        private CustomDiscordResult() { }

        public CommandError? Error { private set; get; }
        public string? ErrorReason { private set; get; }
        public bool IsSuccess => !Error.HasValue;
    }
}