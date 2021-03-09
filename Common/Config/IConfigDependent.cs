using System.Threading.Tasks;

namespace Common.Config
{
    public interface IConfigDependent
    {
        public Task OnConfigLoaded();
    }
}