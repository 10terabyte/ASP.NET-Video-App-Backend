using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoAppBackend.Data;
using VideoAppBackend.Models;

namespace VideoAppBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VideoController: ControllerBase
    {
        private readonly DataContext _context;

        public VideoController(DataContext context) { 
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Video>>> GetVideos()
        {
            return await _context.Videos.ToListAsync();
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadVideo([FromForm] string title, [FromForm] string description, [FromForm] List<string> categories, [FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded" });


            var approvedExtensions = new[] { ".mp4", ".avi", ".mov" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!approvedExtensions.Contains(fileExtension))
            {
                return BadRequest(new { message = "Invalid file type. Only MP4, AVI, and MOV files are allowed." });
            }

            categories = categories.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var uploadsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
            if (!Directory.Exists(uploadsDirectory))
            {
                Directory.CreateDirectory(uploadsDirectory);
            }
            var filePath = Path.Combine(uploadsDirectory, file.FileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var thumbnailPath = Path.Combine("Thumbnails", file.FileName + "_thumb.jpg");

            var video = new Video
            {
                Title = title,
                Description = description,
                Categories = categories,
                FilePath = filePath,
                ThumbnailPath = thumbnailPath
            };

            _context.Videos.Add(video);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Video uploaded successfully", videoId = video.Id });
        }
    }
}
