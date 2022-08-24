namespace Etherna.BeehiveManager.Areas.Api.DtoModels
{
    public enum PinnedResourceStatusDto
    {
        NotPinned,
        InProgress,
        Pinned
    }

    public class PinnedResourceDto
    {
        public PinnedResourceDto(
            string hash,
            string nodeId,
            PinnedResourceStatusDto status)
        {
            Hash = hash;
            NodeId = nodeId;
            Status = status;
        }

        public string Hash { get; }
        public string NodeId { get; }
        public PinnedResourceStatusDto Status { get; }
    }
}
