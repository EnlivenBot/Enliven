using System.Threading.Tasks;
using Common.Localization.Entries;

namespace Bot.DiscordRelated.Commands.Modules {
    public interface IRepliedEntry {
        public Task ChangeEntryAsync(IEntry entry);
        public Task DeleteAsync();
    }
}