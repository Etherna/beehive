namespace Etherna.BeehiveManager.Areas.Api.DtoModels
{
    public class PostageBatchDto
    {
        // Constructors.
        public PostageBatchDto(
            string id,
            int depth,
            long plurAmount)
        {
            Id = id;
            Depth = depth;
            PlurAmount = plurAmount;
        }

        // Properties.
        public string Id { get; }
        public int Depth { get; }
        public long PlurAmount { get; }
    }
}
