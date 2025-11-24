using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using System;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
using System.Threading.Tasks;

namespace Web.Pages
{
    [RequestSizeLimit(10 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
    public class IndexModel : PageModel
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly Options _options;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(IHttpClientFactory httpFactory, Options options, ILogger<IndexModel> logger)
        {
            _httpFactory = httpFactory;
            _options = options;
            _logger = logger;
            this.ImageList = new List<string>();
        }

        [BindProperty]
        public List<string> ImageList { get; private set; }

        [BindProperty]
        public IFormFile Upload { get; set; }

        public async Task OnGetAsync()
        {
            var imagesUrl = _options?.ApiUrl;

            // If ApiUrl is not configured, avoid calling HttpClient and return an empty list.
            if (string.IsNullOrWhiteSpace(imagesUrl))
            {
                this.ImageList = new List<string>();
                return;
            }

            try
            {
                var client = _httpFactory.CreateClient("images");
                string imagesJson = await client.GetStringAsync(imagesUrl);
                IEnumerable<string> imagesList = JsonConvert.DeserializeObject<IEnumerable<string>>(imagesJson);
                this.ImageList = imagesList?.ToList() ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get image list from {ImagesUrl}", imagesUrl);
                this.ImageList = new List<string>();
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (Upload != null && Upload.Length > 0)
            {
                var imagesUrl = _options?.ApiUrl;

                // If ApiUrl is not configured, skip the upload to avoid throwing an exception.
                if (string.IsNullOrWhiteSpace(imagesUrl))
                {
                    TempData["Error"] = "Upload endpoint is not configured.";
                    return RedirectToPage("/Index");
                }

                // Basic server-side validations
                const long MaxFileSize = 10 * 1024 * 1024; // 10 MB
                var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };

                if (Upload.Length > MaxFileSize)
                {
                    TempData["Error"] = "ไม่ได้นะ";
                    return RedirectToPage("/Index");
                }

                var fileExt = Path.GetExtension(Upload.FileName ?? string.Empty).ToLowerInvariant();
                if (string.IsNullOrEmpty(fileExt) || !allowedExt.Contains(fileExt))
                {
                    TempData["Error"] = "Invalid file extension.";
                    return RedirectToPage("/Index");
                }

                if (string.IsNullOrWhiteSpace(Upload.ContentType) || !Upload.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Error"] = "Invalid content type. Only image files are allowed.";
                    return RedirectToPage("/Index");
                }

                try
                {
                    using (var ms = new MemoryStream())
                    {
                        await Upload.CopyToAsync(ms);
                        ms.Position = 0;

                        if (!IsImageBySignature(ms))
                        {
                            TempData["Error"] = "Uploaded file is not a valid image.";
                            return RedirectToPage("/Index");
                        }

                        ms.Position = 0;
                        var client = _httpFactory.CreateClient("images");

                        using (var multipart = new MultipartFormDataContent())
                        {
                            var streamContent = new StreamContent(ms);
                            streamContent.Headers.ContentType = new MediaTypeHeaderValue(Upload.ContentType);
                            multipart.Add(streamContent, "file", Path.GetFileName(Upload.FileName ?? "upload"));

                            var response = await client.PostAsync(imagesUrl, multipart);
                            if (response.IsSuccessStatusCode)
                            {
                                TempData["Success"] = "Upload successful.";
                            }
                            else
                            {
                                TempData["Error"] = $"Upload failed (status {response.StatusCode}).";
                                _logger.LogWarning("Upload to {ImagesUrl} returned {Status}", imagesUrl, response.StatusCode);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Network error during upload to {ImagesUrl}", imagesUrl);
                    TempData["Error"] = "Network error during upload.";
                }
            }
            return RedirectToPage("/Index");
        }

        private bool IsImageBySignature(Stream stream)
        {
            // Reads header bytes and checks common image signatures. Leaves stream position after call undefined -> caller should reset.
            try
            {
                if (!stream.CanRead)
                    return false;

                byte[] header = new byte[12];
                int read = stream.Read(header, 0, header.Length);
                stream.Position = 0;

                if (read >= 2 && header[0] == 0xFF && header[1] == 0xD8)
                    return true; // JPEG
                if (read >= 4 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
                    return true; // PNG
                if (read >= 4 && header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38)
                    return true; // GIF
                if (read >= 2 && header[0] == 0x42 && header[1] == 0x4D)
                    return true; // BMP
                if (read >= 12 && header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F'
                    && header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P')
                    return true; // WEBP

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}