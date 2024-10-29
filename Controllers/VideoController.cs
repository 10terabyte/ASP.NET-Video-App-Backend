using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoAppBackend.Data;
using VideoAppBackend.Models;
using System.Drawing;
using FFMpegCore;
namespace VideoAppBackend.Controllers
{
    [Route("api/videos")]
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

            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";

            var uploadsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
            if (!Directory.Exists(uploadsDirectory))
            {
                Directory.CreateDirectory(uploadsDirectory);
            }

            var filePath = Path.Combine(uploadsDirectory, uniqueFileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var thumbnailsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Thumbnails");
            if (!Directory.Exists(thumbnailsDirectory))
            {
                Directory.CreateDirectory(thumbnailsDirectory);
            }

            var thumbnailFileName = $"{Path.GetFileNameWithoutExtension(uniqueFileName)}_thumb";
            var thumbnailPath = Path.Combine(thumbnailsDirectory, thumbnailFileName);

            try
            {
                FFMpeg.Snapshot(filePath, thumbnailPath, new Size(256, 256), TimeSpan.FromSeconds(1));
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Thumbnail generation failed", error = ex.Message });
            }

            var video = new Video
            {
                Title = title,
                Description = description,
                Categories = categories,
                FilePath = uniqueFileName,         
                ThumbnailPath = "thumb/" + thumbnailFileName  + ".png"
            };

            _context.Videos.Add(video);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Video uploaded successfully", videoId = video.Id });
        }



    }
}
