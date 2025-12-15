using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Text.RegularExpressions;

namespace Superchef.Services;

public class ImageService
{
    private readonly IWebHostEnvironment en;

    public ImageService(IWebHostEnvironment en)
    {
        this.en = en;
    }

    public string ValidateImage(IFormFile f, int maxSize)
    {
        if (f == null || f.Length == 0) return "No image uploaded.";

        var reType = new Regex(@"^image\/(jpeg|png)$", RegexOptions.IgnoreCase);
        var reName = new Regex(@"^.+\.(jpeg|jpg|png)$", RegexOptions.IgnoreCase);

        if (!reType.IsMatch(f.ContentType) || !reName.IsMatch(f.FileName))
        {
            return "Only JPG and PNG image is allowed.";
        }
        else if (f.Length > maxSize * 1024 * 1024)
        {
            return $"Image size cannot more than {maxSize}MB.";
        }

        return "";
    }

    public string SaveImage(
        IFormFile f, string folder,
        double outputWidth, double outputHeight,
        double positionXPercent, double positionYPercent, double scale)
    {
        var file = Guid.NewGuid().ToString("n") + ".jpg";
        var path = Path.Combine(en.WebRootPath, "uploads", folder, file);

        using var stream = f.OpenReadStream();
        using var img = Image.Load<Rgba32>(stream);

        // Special crop
        try
        {
            SpecialCrop(img, outputWidth, outputHeight, positionXPercent, positionYPercent, scale);
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }

        img.Mutate(x => x.BackgroundColor(Color.White)); // Flatten transparency to white before saving
        img.Save(path);

        return file;
    }

    public void DeleteImage(string file, string folder)
    {
        file = Path.GetFileName(file);
        var path = Path.Combine(en.WebRootPath, "uploads", folder, file);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /*
        outputWidth: final image width
        outputHeight: final image height
        positionXPercent: horizontal offset from center (-50 to +50%) of the resized image
        positionYPercent: vertical offset from center (-50 to +50%) of the resized image
        scale: scale applied to the image
    */
    public void SpecialCrop(Image image, double outputWidth, double outputHeight, double positionXPercent, double positionYPercent, double scale)
    {
        // Resize the image according to scale while keeping aspect ratio
        double currentRatio = (double)image.Height / image.Width;
        double targetRatio = outputHeight / outputWidth;

        int resizedWidth, resizedHeight;

        if (targetRatio > currentRatio)
        {
            // Fit by height
            resizedHeight = (int)Math.Round(outputHeight * scale);
            resizedWidth = (int)Math.Round(resizedHeight / currentRatio);
        }
        else
        {
            // Fit by width
            resizedWidth = (int)Math.Round(outputWidth * scale);
            resizedHeight = (int)Math.Round(resizedWidth * currentRatio);
        }

        image.Mutate(x => x.Resize(resizedWidth, resizedHeight));

        // Convert percentage offsets to pixels relative to resized image
        double posX = positionXPercent / scale / 100.0 * resizedWidth;
        double posY = positionYPercent / scale / 100.0 * resizedHeight;

        // Calculate crop rectangle (centered)
        int cropX = (int)Math.Round((resizedWidth / 2) - (outputWidth / 2) - posX);
        int cropY = (int)Math.Round((resizedHeight / 2) - (outputHeight / 2) - posY);

        int cropW = (int)Math.Round(outputWidth);
        int cropH = (int)Math.Round(outputHeight);

        // Clamp crop
        cropX = Math.Clamp(cropX, 0, resizedWidth - cropW); // Math.Clamp(value, min, max)
        cropY = Math.Clamp(cropY, 0, resizedHeight - cropH);

        // Final check
        cropW = Math.Min(cropW, resizedWidth);
        cropH = Math.Min(cropH, resizedHeight);

        image.Mutate(x => x.Crop(new Rectangle(cropX, cropY, cropW, cropH)));
    }
}