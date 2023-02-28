using Etherna.BeehiveManager.Domain.Models;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Services.Domain
{
    public interface IBeeNodeService
    {
        Task<BeeNode> SelectRandomHealthyNodeAsync();
    }
}