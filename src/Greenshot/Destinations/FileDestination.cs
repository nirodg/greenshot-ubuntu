using Greenshot.Base.Core;
using Greenshot.Base.Core.Enums;
using Greenshot.Base.Interfaces;

namespace Greenshot.Destinations;

public class FileDestination : IDestination
{
    private readonly ICoreConfiguration _config;

    public FileDestination(ICoreConfiguration config) => _config = config;

    public string Name => "FileDefault";
    public string Description => "Save to file";
    public int Priority => 2;
    public bool IsActive => true;

    public async Task<ExportInformation> ExportCaptureAsync(
        bool manuallyInitiated, ICapture capture, CancellationToken ct = default)
    {
        if (capture.Image == null)
            return new ExportInformation(false, ErrorMessage: "No image");

        try
        {
            Directory.CreateDirectory(_config.OutputFilePath);

            var filename = FilenameHelper.FillPattern(_config.OutputFileFilenamePattern, capture.CaptureDetails);
            var ext = ImageSaveHelper.GetExtension(_config.OutputFileFormat);

            string path;
            if (_config.OutputFileAllowOverwrite)
                path = Path.Combine(_config.OutputFilePath, filename + ext);
            else
                path = FilenameHelper.GetUniqueFilename(_config.OutputFilePath, filename, ext);

            await ImageSaveHelper.SaveAsync(capture.Image, path, _config.OutputFileFormat, _config.OutputFileJpegQuality);
            return new ExportInformation(true, FilePath: path);
        }
        catch (Exception ex)
        {
            return new ExportInformation(false, ErrorMessage: ex.Message);
        }
    }
}
