using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoAppBackend.Data;
using VideoAppBackend.Models;
using System.Drawing;
using FFMpegCore;
using System.Diagnostics;
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
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadVideo([FromForm] string title, [FromForm] string description, [FromForm] List<string> categories, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded" });

            var approvedMimeTypes = new[] { "video/mp4", "video/avi", "video/mov", "video/x-msvideo", "video/quicktime" };

            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!approvedMimeTypes.Contains(file.ContentType))
            {
                Debug.WriteLine(file.ContentType);
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
            string mp4FilePath = filePath;
            if (file.ContentType != "video/mp4")
            {
                mp4FilePath = Path.Combine(uploadsDirectory, $"{Path.GetFileNameWithoutExtension(uniqueFileName)}.mp4");
                try
                {
                    FFMpegCore.FFMpegArguments
                        .FromFileInput(filePath)
                        .OutputToFile(mp4FilePath, overwrite: true, options => options
                            .WithVideoCodec("libx264")
                            .WithConstantRateFactor(21)
                            .WithAudioCodec("aac")
                            .WithFastStart())
                        .ProcessSynchronously();

                    System.IO.File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    return BadRequest(new { message = "File conversion failed", error = ex.Message });
                }
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
                FFMpeg.Snapshot(mp4FilePath, thumbnailPath, new Size(256, 256), TimeSpan.FromSeconds(1));
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
                FilePath = mp4FilePath,         
                ThumbnailPath = "thumb/" + thumbnailFileName  + ".png"
            };

            _context.Videos.Add(video);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Video uploaded successfully", videoId = video.Id });
        }

        [HttpGet("stream/{videoId}")]
        public async Task<IActionResult> StreamVideo(int videoId)
        {
            var video = await _context.Videos.FindAsync(videoId);

            if (video == null)
                return NotFound(new { message = "Video not found" });

            var videoPath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", video.FilePath);

            if (!System.IO.File.Exists(videoPath))
                return NotFound(new { message = "Video file not found" });

            var fileStream = new FileStream(videoPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var fileLength = fileStream.Length;

            Response.Headers.Append("Accept-Ranges", "bytes");

            if (Request.Headers.ContainsKey("Range"))
            {
                var rangeHeader = Request.Headers["Range"].ToString();
                var range = rangeHeader.Replace("bytes=", "").Split('-');
                var start = long.Parse(range[0]);
                var end = range.Length > 1 && !string.IsNullOrEmpty(range[1])
                    ? long.Parse(range[1])
                    : fileLength - 1;

                if (start >= fileLength || end >= fileLength || start > end)
                    return BadRequest(new { message = "Invalid range" });

                fileStream.Seek(start, SeekOrigin.Begin);

                Response.StatusCode = 206;
                Response.Headers.Append("Content-Range", $"bytes {start}-{end}/{fileLength}");
                var length = end - start + 1;

                return File(fileStream, "video/mp4", enableRangeProcessing: true);
            }
            else
            {
                return File(fileStream, "video/mp4");
            }
        }


    }
}
