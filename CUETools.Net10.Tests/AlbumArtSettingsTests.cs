using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using CUETools.Processor;
using CUETools.Processor.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CUETools.Net10.Tests;
[TestClass]
public class AlbumArtSettingsTests
{
    [TestMethod]
    public void DefaultAndSavedResizeLimitsAreRespected()
    {
        var config = new CUEConfig();
        Assert.AreEqual(500, config.maxAlbumArtSize);
        var dir = Path.Combine(Path.GetTempPath(), "cuetools-art-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            config.maxAlbumArtSize = 300;
            var app = Path.Combine(dir, "app.exe");
            var writer = new SettingsWriter("Test", "settings.txt", app);
            config.Save(writer);
            writer.Close();
            var restored = new CUEConfig();
            restored.Load(new SettingsReader("Test", "settings.txt", app));
            Assert.AreEqual(300, restored.maxAlbumArtSize);
        }
        finally { Directory.Delete(dir, true); }
    }
    [TestMethod]
    public void JpegWriterUsesMaximumQuality()
    {
        using var image = new Bitmap(64, 64);
        for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
                image.SetPixel(x, y, Color.FromArgb((x * 17 + y * 7) % 256, (x * 3 + y * 19) % 256, (x * y) % 256));
        using var actual = new MemoryStream();
        CUESheet.SaveJpegAtMaximumQuality(image, actual);
        using var expected = new MemoryStream();
        var codec = Array.Find(ImageCodecInfo.GetImageEncoders(), item => item.FormatID == ImageFormat.Jpeg.Guid);
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(Encoder.Quality, 100L);
        image.Save(expected, codec, parameters);
        CollectionAssert.AreEqual(expected.ToArray(), actual.ToArray());
    }
    [TestMethod]
    public void SelectedCoverReplacesExistingArtForTheCurrentAlbum()
    {
        var cue = new CUESheet(new CUEConfig());
        using var oldImage = new Bitmap(4, 4);
        using var selectedImage = new Bitmap(6, 5);
        using var oldBytes = new MemoryStream();
        using var selectedBytes = new MemoryStream();
        oldImage.Save(oldBytes, ImageFormat.Png); selectedImage.Save(selectedBytes, ImageFormat.Png);
        cue.AlbumArt.Add(new TagLib.Picture(new TagLib.ByteVector(oldBytes.ToArray())));
        var selected = new TagLib.Picture(new TagLib.ByteVector(selectedBytes.ToArray()));
        cue.UseSelectedAlbumArt(selected);
        Assert.AreEqual(1, cue.AlbumArt.Count);
        Assert.AreSame(selected, cue.AlbumArt[0]);
        Assert.AreEqual(TagLib.PictureType.FrontCover, cue.AlbumArt[0].Type);
        cue.Action = CUEAction.Encode;
        cue.LoadAndResizeAlbumArt();
        Assert.AreSame(selected, cue.AlbumArt[0], "Explicit cover must not be replaced by another source.");
        CollectionAssert.AreEqual(selectedBytes.ToArray(), cue.AlbumArt[0].Data.Data);
    }
}
