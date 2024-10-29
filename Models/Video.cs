namespace VideoAppBackend.Models
{
    public class Video
    {
        public int Id { get; set; }
        public required string Title { get; set; }
        public required string Description { get; set; }
        public List<string> Categories { get; set; } = [];
        public required string FilePath { get; set; }
        public required string ThumbnailPath { get; set; }
    }
}
