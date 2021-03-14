using System.Threading.Tasks;
using Discord;

namespace Bot.Utilities.Collector {
    public class MessageCollectorEventArgs : CollectorEventArgsBase {
        public IMessage Message { get; set; }

        public MessageCollectorEventArgs(CollectorController controller, IMessage message) : base(controller) {
            Message = message;
        }

        public override async Task RemoveReason() {
            try {
                await Message.DeleteAsync();
            }
            catch {
                Controller.OnRemoveArgsFailed(this);
            }
        }
    }
}