namespace Etherna.BeehiveManager.Areas.Api.DtoModels
{
    public class PostageBatchRefDto
    {
        public PostageBatchRefDto(
            string batchId,
            string nodeId)
        {
            BatchId = batchId;
            NodeId = nodeId;
        }

        public string BatchId { get; }
        public string NodeId { get; }
    }
}
