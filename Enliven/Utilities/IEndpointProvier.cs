using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;

namespace Bot.Utilities;

public interface IEndpointProvider {
    Task ConfigureEndpoints(WebApplication app);
}