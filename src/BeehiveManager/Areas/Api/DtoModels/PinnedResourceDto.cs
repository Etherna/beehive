namespace Etherna.BeehiveManager.Areas.Api.DtoModels
{
    public class PinnedResourceDto
    {
        public PinnedResourceDto(
            string hash,
            bool isPinned,
            string nodeId)
        {
            Hash = hash;
            IsPinned = isPinned;
            NodeId = nodeId;
        }

        public string Hash { get; }
        public bool IsPinned { get; }
        public string NodeId { get; }
    }
}
