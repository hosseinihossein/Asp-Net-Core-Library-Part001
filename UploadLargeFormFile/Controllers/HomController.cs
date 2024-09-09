using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using UploadLargeFormFile.Filters;
using UploadLargeFormFile.Models;

namespace UploadLargeFormFile.Controllers;

public class HomeController : Controller
{
    readonly IWebHostEnvironment env;
    readonly char ds = Path.DirectorySeparatorChar;

    public HomeController(IWebHostEnvironment _env)
    {
        env = _env;
    }

    public IActionResult Index(string? fileGuid)
    {
        if (fileGuid is not null)
        {
            object? o = TempData[fileGuid];
            ViewBag.ResultState = "info";
            return View("Result", o);
        }
        return View();
    }

    [GenerateAntiforgeryTokenCookie]
    public IActionResult AddFile()
    {
        return View(new Downloads_FormModel());
    }

    [HttpPost]
    [DisableRequestSizeLimit]
    [DisableFormValueModelBinding]
    public async Task<IActionResult> SubmitNewFile()
    {
        if (!await HttpContext.RequestServices.GetService<IAntiforgery>()!.IsRequestValidAsync(HttpContext))
        {
            ModelState.AddModelError("antiforgerytoken", "anti forgery token is incorrect!");
            return BadRequest(ModelState);
        }

        if (!string.IsNullOrEmpty(Request.ContentType) &&
            !Request.ContentType.Contains("multipart/", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("File",
                $"The request couldn't be processed (Error 1).");
            ModelState.AddModelError("Request.ContentType",
            $"{Request.ContentType}");
            // Log error

            return BadRequest(ModelState);
        }

        if (!Request.HasFormContentType ||
            !MediaTypeHeaderValue.TryParse(Request.ContentType, out var mediaTypeHeader) ||
            string.IsNullOrEmpty(mediaTypeHeader.Boundary.Value))
        {
            return new UnsupportedMediaTypeResult();
        }

        // Accumulate the form data key-value pairs in the request (formAccumulator).
        var formAccumulator = new KeyValueAccumulator();
        /*var trustedFileNameForDisplay = string.Empty;
        var untrustedFileNameForStorage = string.Empty;
        var streamedFileContent = Array.Empty<byte>();*/

        var boundary = HeaderUtilities.RemoveQuotes(mediaTypeHeader.Boundary).Value;
        if (string.IsNullOrWhiteSpace(boundary))
        {
            throw new InvalidDataException("Missing content-type boundary.");
        }

        if (boundary.Length > 70)
        {
            throw new InvalidDataException(
                $"Multipart boundary length limit {70} exceeded.");
        }

        var reader = new MultipartReader(boundary, HttpContext.Request.Body);

        var section = await reader.ReadNextSectionAsync();
        string? uploadedFileName = null;
        string? uploadedFileImageName = null;
        string fileGuid = Guid.NewGuid().ToString().Replace("-", "");

        while (section != null)
        {
            var hasContentDispositionHeader =
                ContentDispositionHeaderValue.TryParse(
                    section.ContentDisposition, out var contentDisposition);

            if (hasContentDispositionHeader)
            {
                var fileDirInfo = Directory.CreateDirectory(Path.Combine(env.WebRootPath, "Files", fileGuid));
                if (contentDisposition != null
                    && contentDisposition.DispositionType.Equals("form-data")
                    && (!string.IsNullOrEmpty(contentDisposition.FileName.Value)
                        || !string.IsNullOrEmpty(contentDisposition.FileNameStar.Value)))
                {
                    var fileName = WebUtility.HtmlEncode(contentDisposition.FileName.Value);
                    fileName ??= "fileName";
                    var key = HeaderUtilities.RemoveQuotes(contentDisposition.Name).Value;
                    string? saveToPath = null;

                    if (key == "File")
                    {
                        uploadedFileName = fileName;
                        saveToPath = Path.Combine(fileDirInfo.FullName, fileName);
                    }
                    if (key == "FileImage")
                    {
                        uploadedFileImageName = fileName;
                        saveToPath = $"{env.WebRootPath}{ds}Images{ds}Files{ds}{fileGuid}";
                    }

                    if (saveToPath is null)
                    {
                        ModelState.AddModelError("saveToPath",
                        "saveToPath is null (Error 6).");
                        // Log error

                        return BadRequest(ModelState);
                    }

                    using (var fs = System.IO.File.Create(saveToPath))
                    {
                        await section.Body.CopyToAsync(fs);
                    }
                }
                else if (contentDisposition != null
                        && contentDisposition.DispositionType.Equals("form-data")
                        && string.IsNullOrEmpty(contentDisposition.FileName.Value)
                        && string.IsNullOrEmpty(contentDisposition.FileNameStar.Value))
                {
                    // Don't limit the key name length because the 
                    // multipart headers length limit is already in effect.
                    var key = HeaderUtilities
                        .RemoveQuotes(contentDisposition.Name).Value;

                    var hasMediaTypeHeader =
                    MediaTypeHeaderValue.TryParse(section.ContentType, out var mediaType);

                    using (var streamReader = new StreamReader(
                        section.Body,
                        mediaType?.Encoding ?? Encoding.UTF8,
                        detectEncodingFromByteOrderMarks: true,
                        bufferSize: 1024,
                        leaveOpen: true))
                    {
                        // The value length limit is enforced by 
                        // MultipartBodyLengthLimit
                        var value = await streamReader.ReadToEndAsync();

                        if (string.Equals(value, "undefined",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            value = string.Empty;
                        }

                        formAccumulator.Append(key!, value);
                    }
                }
            }

            // Drain any remaining section body that hasn't been consumed and
            // read the headers for the next section.
            section = await reader.ReadNextSectionAsync();
        }

        // Bind form data to the model
        //var formData = new FormData();
        var formModel = new Downloads_FormModel();
        var formValueProvider = new FormValueProvider(
            BindingSource.Form,
            new FormCollection(formAccumulator.GetResults()),
            CultureInfo.CurrentCulture);

        var bindingSuccessful = await TryUpdateModelAsync(formModel, prefix: "",
            valueProvider: formValueProvider);

        if (!bindingSuccessful)
        {
            ModelState.AddModelError("File",
                "The request couldn't be processed (Error 5).");
            // Log error

            return BadRequest(ModelState);
        }

        string fileImagePath = $"{env.WebRootPath}{ds}Images{ds}Files{ds}{fileGuid}";
        string largeFilePath = Path.Combine(env.WebRootPath, "Files", fileGuid, uploadedFileName ?? string.Empty);

        FileInfo? largeFileInfo = null;
        if (System.IO.File.Exists(largeFilePath))
        {
            largeFileInfo = new FileInfo(largeFilePath);
        }

        FileInfo? imageFileInfo = null;
        if (System.IO.File.Exists(fileImagePath))
        {
            imageFileInfo = new FileInfo(fileImagePath);
        }

        string o = $"<p>Guid = {fileGuid}\n</p>" +
            $"<p>Title = {formModel.Title}\n</p>" +
            $"<p>Description = {formModel.Description ?? string.Empty}\n</p>" +
            $"<p>Category = {formModel.Category}\n</p>" +
            $"<p>Large File Name = {uploadedFileName ?? string.Empty}\n</p>" +
            $"<p>Large File Size = {(largeFileInfo?.Length / (1024.0 * 1024.0))?.ToString("F2") ?? "0"} MB</p>" +
            $"<p>Image File Name = {uploadedFileImageName ?? string.Empty}\n</p>" +
            $"<p>Image File Size = {(imageFileInfo?.Length / 1024.0)?.ToString("F2") ?? "0"} KB</p>";

        TempData[fileGuid] = o;
        return Ok($"/Home/Index?fileGuid={fileGuid}");
    }
}