using System.Threading.Tasks;

namespace Common {
    public interface IService {
        Task OnPreDiscordLoginInitialize() {
            return Task.CompletedTask;
        }
        
        Task OnPreDiscordStartInitialize() {
            return Task.CompletedTask;
        }
        
        Task OnPostDiscordStartInitialize() {
            return Task.CompletedTask;
        }

        Task OnShutdown(bool isDiscordStarted) {
            return Task.CompletedTask;
        }
    }
}