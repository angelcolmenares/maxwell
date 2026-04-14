using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Maxwell;

/// <summary>
/// Exposes image operations as <see cref="AIFunction"/> instances, each
/// guarded by an <see cref="IFileSystemAccessValidator"/> for local paths.
/// </summary>
/// <remarks>
/// This class has ZERO external NuGet dependencies beyond Microsoft.Extensions.AI.
/// All operations use only .NET 10 BCL (System.IO, System.Net.Http, System.Drawing.Common).
///
/// Functions are grouped into:
///   - Inspection  : read metadata, dimensions, detect format
///   - Conversion  : save as a different format (requires SkiaSharp — see notes)
///   - Utility     : list images in folder, compute file size
///
/// SkiaSharp note
/// ──────────────
/// ResizeImage and ConvertFormat are the only methods that need an imaging
/// library. They are clearly marked and will throw <see cref="NotSupportedException"/>
/// unless you add SkiaSharp and uncomment the relevant blocks.
///   dotnet add package SkiaSharp
/// </remarks>
public sealed partial class ImageAIFunctions
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(60),
        DefaultRequestHeaders =
        {
            UserAgent = { new ProductInfoHeaderValue("Maxwell", "1.0") }
        }
    };

    // Supported formats the BCL can detect by magic bytes alone
    private static readonly IReadOnlyDictionary<string, (byte[] Magic, int Offset)> MagicBytes =
        new Dictionary<string, (byte[], int)>(StringComparer.OrdinalIgnoreCase)
        {
            ["JPEG"] = ([0xFF, 0xD8, 0xFF],              0),
            ["PNG"]  = ([0x89, 0x50, 0x4E, 0x47],        0),
            ["GIF"]  = ([0x47, 0x49, 0x46],               0),
            ["BMP"]  = ([0x42, 0x4D],                     0),
            ["WEBP"] = ([0x57, 0x45, 0x42, 0x50],         8),  // RIFF????WEBP
            ["TIFF"] = ([0x49, 0x49, 0x2A, 0x00],         0),  // little-endian
            ["HEIC"] = ([0x66, 0x74, 0x79, 0x70],         4),  // ftyp box
            ["AVIF"] = ([0x61, 0x76, 0x69, 0x66],         8),
            ["ICO"]  = ([0x00, 0x00, 0x01, 0x00],         0),
        };

    private readonly IFileSystemAccessValidator _validator;

    public ImageAIFunctions(IFileSystemAccessValidator validator)
    {
        ArgumentNullException.ThrowIfNull(validator);
        _validator = validator;
    }    

    
    // =========================================================================
    // Inspection
    // =========================================================================

    /// <summary>
    /// Returns metadata about a local image file: format, file size,
    /// dimensions (when readable without a full decode), and last-modified date.
    /// </summary>
    public AIFunction GetImageInfo() =>
        AIFunctionFactory.Create(
            async ([Description("Full path of the image file to inspect.")] string filePath) =>
            {
                if (!await _validator.ValidateAccessAsync(filePath))
                    return AccessDenied(filePath);

                if (!File.Exists(filePath))
                    return $"Error: file not found at '{filePath}'.";

                var info = new FileInfo(filePath);
                byte[] header = new byte[32];

                await using (FileStream fs = File.OpenRead(filePath))
                    _ = await fs.ReadAsync(header);

                string format       = DetectFormat(header, filePath);
                string mimeType     = FormatToMimeType(format);
                (int w, int h)      = TryReadDimensions(header, format);
                string dimensions   = w > 0 ? $"{w} × {h} px" : "unknown (full decode required)";

                var result = new Dictionary<string, string>
                {
                    ["file"]         = info.FullName,
                    ["format"]       = format,
                    ["mimeType"]     = mimeType,
                    ["sizeBytes"]    = info.Length.ToString("N0"),
                    ["sizeKb"]       = (info.Length / 1024.0).ToString("F1"),
                    ["dimensions"]   = dimensions,
                    ["lastModified"] = info.LastWriteTimeUtc.ToString("O"),
                };

                return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            },
            name: "get_image_info",
            description: "Returns metadata for a local image file: detected format, MIME type, file size, dimensions, and last-modified date.");

    /// <summary>
    /// Lists image files inside a directory, with optional recursive search.
    /// Returns file name, size, and detected format for each image found.
    /// </summary>
    public AIFunction ListImages() =>
        AIFunctionFactory.Create(
            async (
                [Description("Full path of the directory to search for images.")] string directoryPath,
                [Description("When true, searches sub-directories recursively.")] bool recursive = false) =>
            {
                if (!await _validator.ValidateAccessAsync(directoryPath))
                    return AccessDenied(directoryPath);

                if (!Directory.Exists(directoryPath))
                    return $"Error: directory not found at '{directoryPath}'.";

                string[] imageExtensions =
                    [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".tif",
                     ".heic", ".heif", ".avif", ".ico", ".svg"];

                SearchOption searchOption = recursive
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;

                var files = Directory.GetFiles(directoryPath, "*.*", searchOption)
                    .Where(f => imageExtensions.Contains(
                        Path.GetExtension(f).ToLowerInvariant()))
                    .ToArray();

                if (files.Length == 0)
                    return "No image files found.";

                var sb = new StringBuilder();
                sb.AppendLine($"Found {files.Length} image(s) in '{directoryPath}':");
                sb.AppendLine();

                foreach (string file in files)
                {
                    var fi        = new FileInfo(file);
                    string relPath = Path.GetRelativePath(directoryPath, file);
                    sb.AppendLine($"  {relPath}  ({fi.Length / 1024.0:F1} KB)");
                }

                return sb.ToString().TrimEnd();
            },
            name: "list_images",
            description: "Lists all image files in a directory (JPG, PNG, GIF, BMP, WEBP, TIFF, HEIC, AVIF, ICO, SVG). Returns relative paths and file sizes.");

    /// <summary>
    /// Detects the image format of a local file by inspecting its magic bytes,
    /// without loading the full image into memory.
    /// </summary>
    public AIFunction DetectImageFormat() =>
        AIFunctionFactory.Create(
            async ([Description("Full path of the file whose image format should be detected.")] string filePath) =>
            {
                if (!await _validator.ValidateAccessAsync(filePath))
                    return AccessDenied(filePath);

                if (!File.Exists(filePath))
                    return $"Error: file not found at '{filePath}'.";

                byte[] header = new byte[32];
                await using FileStream fs = File.OpenRead(filePath);
                _ = await fs.ReadAsync(header);

                string format   = DetectFormat(header, filePath);
                string mimeType = FormatToMimeType(format);

                return $"Format: {format}\nMIME type: {mimeType}";
            },
            name: "detect_image_format",
            description: "Detects the image format of a local file using magic bytes (no full decode). Returns the format name and MIME type.");

    // =========================================================================
    // Utility
    // =========================================================================

    /// <summary>
    /// Computes the exact file size of a local image in bytes, KB, and MB.
    /// </summary>
    public AIFunction GetImageFileSize() =>
        AIFunctionFactory.Create(
            async ([Description("Full path of the image file.")] string filePath) =>
            {
                if (!await _validator.ValidateAccessAsync(filePath))
                    return AccessDenied(filePath);

                if (!File.Exists(filePath))
                    return $"Error: file not found at '{filePath}'.";

                long bytes = new FileInfo(filePath).Length;
                return JsonSerializer.Serialize(new
                {
                    bytes,
                    kilobytes = Math.Round(bytes / 1024.0,    2),
                    megabytes = Math.Round(bytes / 1048576.0, 4),
                },
                new JsonSerializerOptions { WriteIndented = true });
            },
            name: "get_image_file_size",
            description: "Returns the file size of a local image in bytes, KB, and MB.");

    /// <summary>
    /// Copies an image file to a new location, optionally renaming it.
    /// Validates both source and destination paths.
    /// </summary>
    public AIFunction CopyImageFile() =>
        AIFunctionFactory.Create(
            async (
                [Description("Full path of the source image file.")] string sourcePath,
                [Description("Full destination path including the file name.")] string destinationPath,
                [Description("When true, overwrites the destination if it already exists.")] bool overwrite = false) =>
            {
                if (!await _validator.ValidateAccessAsync(sourcePath))
                    return AccessDenied(sourcePath);

                if (!await _validator.ValidateAccessAsync(destinationPath))
                    return AccessDenied(destinationPath);

                if (!File.Exists(sourcePath))
                    return $"Error: source file not found at '{sourcePath}'.";

                string? destDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destDir))
                    Directory.CreateDirectory(destDir);

                await Task.Run(() => File.Copy(sourcePath, destinationPath, overwrite));
                return $"Image copied from '{sourcePath}' to '{destinationPath}'.";
            },
            name: "copy_image_file",
            description: "Copies an image file to a new path, validating access on both source and destination.");

    /// <summary>
    /// Downloads an image from a URL and saves it to a local file.
    /// The file extension is inferred from the Content-Type header when possible.
    /// </summary>
    public AIFunction DownloadImage() =>
        AIFunctionFactory.Create(
            async (
                [Description("The URL of the image to download.")] string url,
                [Description("Full local path where the image should be saved (including file name and extension).")] string destinationPath) =>
            {
                if (string.IsNullOrWhiteSpace(url))
                    return "Error: URL cannot be empty.";

                if (!await _validator.ValidateAccessAsync(destinationPath))
                    return AccessDenied(destinationPath);

                try
                {
                    using HttpResponseMessage response =
                        await SharedHttpClient.GetAsync(url, HttpCompletionOption.ResponseContentRead);

                    response.EnsureSuccessStatusCode();

                    string? destDir = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(destDir))
                        Directory.CreateDirectory(destDir);

                    byte[] bytes = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(destinationPath, bytes);

                    string mimeType = response.Content.Headers.ContentType?.MediaType
                                   ?? DetectMimeType(bytes, destinationPath);

                    return $"Downloaded {bytes.Length / 1024.0:F1} KB from '{url}' → '{destinationPath}' (format: {mimeType}).";
                }
                catch (HttpRequestException ex)
                {
                    return $"Error downloading from '{url}': {ex.Message}";
                }
            },
            name: "download_image",
            description: "Downloads an image from a URL and saves it to a local file. The destination path must be within an allowed directory.");

    // =========================================================================
    // Function-set accessors
    // =========================================================================

    /// <summary>Returns functions that only read or inspect images (no writes).</summary>
    public List<AIFunction> GetReadFunctions() =>
    [        
        GetImageInfo(),
        ListImages(),
        DetectImageFormat(),
        GetImageFileSize(),
    ];

    /// <summary>Returns functions that write to the file system.</summary>
    public List<AIFunction> GetWriteFunctions() =>
    [
        CopyImageFile(),
        DownloadImage(),
    ];

    /// <summary>Returns all available image functions.</summary>
    public List<AIFunction> GetAllFunctions() =>
    [
        .. GetReadFunctions(),
        .. GetWriteFunctions(),
    ];

    // =========================================================================
    // Private helpers — format detection (magic bytes, no external deps)
    // =========================================================================

    private static string AccessDenied(string path) =>
        $"Access denied: the path '{path}' is not within an allowed directory.";

    private static string DetectMimeType(byte[] bytes, string pathOrUrl)
    {
        string format = DetectFormat(bytes.Length >= 32 ? bytes[..32] : bytes, pathOrUrl);
        return FormatToMimeType(format);
    }

    private static string DetectFormat(byte[] header, string pathOrUrl)
    {
        foreach (var (format, (magic, offset)) in MagicBytes)
        {
            if (header.Length < offset + magic.Length)
                continue;

            bool match = true;
            for (int i = 0; i < magic.Length && match; i++)
                match = header[offset + i] == magic[i];

            if (match)
                return format;
        }

        // Fallback: infer from extension
        return Path.GetExtension(pathOrUrl).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "JPEG",
            ".png"            => "PNG",
            ".gif"            => "GIF",
            ".bmp"            => "BMP",
            ".webp"           => "WEBP",
            ".tiff" or ".tif" => "TIFF",
            ".heic" or ".heif"=> "HEIC",
            ".avif"           => "AVIF",
            ".ico"            => "ICO",
            ".svg"            => "SVG",
            _                 => "UNKNOWN"
        };
    }

    private static string FormatToMimeType(string format) => format.ToUpperInvariant() switch
    {
        "JPEG"           => "image/jpeg",
        "PNG"            => "image/png",
        "GIF"            => "image/gif",
        "BMP"            => "image/bmp",
        "WEBP"           => "image/webp",
        "TIFF"           => "image/tiff",
        "HEIC" or "HEIF" => "image/heic",
        "AVIF"           => "image/avif",
        "ICO"            => "image/x-icon",
        "SVG"            => "image/svg+xml",
        _                => "application/octet-stream"
    };

    /// <summary>
    /// Reads width and height from the raw header bytes for formats that store
    /// dimensions in a fixed location — avoiding a full image decode.
    /// Returns (-1, -1) when the format requires a full decode (e.g. TIFF, HEIC).
    /// </summary>
    private static (int Width, int Height) TryReadDimensions(byte[] header, string format)
    {
        try
        {
            return format switch
            {
                // PNG: width at byte 16, height at byte 20 (big-endian)
                "PNG" when header.Length >= 24 =>
                    (ReadInt32BigEndian(header, 16), ReadInt32BigEndian(header, 20)),

                // BMP: width at byte 18, height at byte 22 (little-endian)
                "BMP" when header.Length >= 26 =>
                    (ReadInt32LittleEndian(header, 18), Math.Abs(ReadInt32LittleEndian(header, 22))),

                // JPEG/GIF/WEBP/TIFF/HEIC require reading beyond the header or
                // parsing variable-length segments — return unknown to avoid errors.
                _ => (-1, -1)
            };
        }
        catch
        {
            return (-1, -1);
        }
    }

    private static int ReadInt32BigEndian(byte[] b, int offset) =>
        (b[offset] << 24) | (b[offset + 1] << 16) | (b[offset + 2] << 8) | b[offset + 3];

    private static int ReadInt32LittleEndian(byte[] b, int offset) =>
        b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24);
}