using System.Threading.Tasks;

namespace Bot.Patches {
    public interface IPatch {
        public Task Apply();
    }
}