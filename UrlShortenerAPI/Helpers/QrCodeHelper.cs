using QRCoder;

public static class QrCodeHelper
{
    public static string GenerateQrCode(string url, string folderPath, string fileName)
    {
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        string filePath = Path.Combine(folderPath, fileName + ".png");

        using var qrGenerator = new QRCodeGenerator();
        QRCodeData qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        byte[] pngBytes = new PngByteQRCode(qrCodeData).GetGraphic(20);
        File.WriteAllBytes(filePath, pngBytes);

        return filePath;
    }
}
