using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

public static class QrCodeHelper
{
    public static string GenerateQrCode(string url, string folderPath, string fileName)
    {
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        string filePath = Path.Combine(folderPath, fileName + ".png");

        using (var qrGenerator = new QRCodeGenerator())
        {
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);

            byte[] qrCodeBytes = new BitmapByteQRCode(qrCodeData).GetGraphic(20);

            using (var ms = new MemoryStream(qrCodeBytes))
            using (var bmp = new Bitmap(ms))
            {
                bmp.Save(filePath, ImageFormat.Png);
            }
        }

        return filePath;
    }
}
