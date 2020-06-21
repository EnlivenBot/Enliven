using Discord.Commands;

namespace Bot.Utilities.Modules {
    public class PatchableModuleBase : ModuleBase {
        public CommandInfo CurrentCommandInfo = null!;
        protected override void BeforeExecute(CommandInfo command) {
            CurrentCommandInfo = command;
            base.BeforeExecute(command);
        }
    }
}