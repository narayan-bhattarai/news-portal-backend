using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace NewsPortal.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UploadsController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public UploadsController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            // 1. Get Supabase settings from environment
            var supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL") ?? _configuration["Supabase:Url"];
            var supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY") ?? _configuration["Supabase:ServiceRoleKey"];
            var bucketName = "news-portal-images"; 

            // Auto-format URL if only the ID was provided (e.g. from the user's recent screenshot)
            if (!string.IsNullOrEmpty(supabaseUrl) && !supabaseUrl.Contains(".") && !supabaseUrl.StartsWith("http"))
            {
                supabaseUrl = $"https://{supabaseUrl}.supabase.co";
                Console.WriteLine($"[Storage] Auto-formatted Supabase ID to URL: {supabaseUrl}");
            }

            // If Supabase is configured, upload there
            if (!string.IsNullOrEmpty(supabaseUrl) && !string.IsNullOrEmpty(supabaseKey))
            {
                try
                {
                    Console.WriteLine($"[Storage] Attempting Supabase upload to bucket: {bucketName}");
                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                    var uploadUrl = $"{supabaseUrl.TrimEnd('/')}/storage/v1/object/{bucketName}/{fileName}";

                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {supabaseKey}");
                        
                        using (var stream = file.OpenReadStream())
                        using (var content = new StreamContent(stream))
                        {
                            content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
                            var response = await client.PostAsync(uploadUrl, content);

                            if (response.IsSuccessStatusCode)
                            {
                                var publicUrl = $"{supabaseUrl.TrimEnd('/')}/storage/v1/object/public/{bucketName}/{fileName}";
                                Console.WriteLine($"[Storage] Supabase upload success: {publicUrl}");
                                return Ok(new { url = publicUrl });
                            }
                            else
                            {
                                var errorResp = await response.Content.ReadAsStringAsync();
                                Console.WriteLine($"[Storage] Supabase upload failed with status {response.StatusCode}: {errorResp}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Storage] Supabase Upload Exception: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("[Storage] Supabase not configured. Using ephemeral local storage fallback.");
            }

            // Fallback: Local upload (Ephemeral on Render)
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var localFileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(uploadsFolder, localFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return Ok(new { url = $"/uploads/{localFileName}" });
        }
    }
}
