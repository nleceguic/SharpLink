using FluentAssertions;

namespace UrlShortenerAPI.Tests.Utils
{
    public class QrCodeHelperTests : IDisposable
    {
        private readonly string _tempFolder;

        public QrCodeHelperTests()
        {
            _tempFolder = Path.Combine(Path.GetTempPath(), "SharpLinkQrTests_" + Guid.NewGuid());
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempFolder))
                Directory.Delete(_tempFolder, recursive: true);
        }

        [Fact]
        public void GenerateQrCode_ShouldCreateFile()
        {
            var filePath = QrCodeHelper.GenerateQrCode("https://example.com", _tempFolder, "test_qr");

            File.Exists(filePath).Should().BeTrue();
        }

        [Fact]
        public void GenerateQrCode_ShouldReturnCorrectFilePath()
        {
            var filePath = QrCodeHelper.GenerateQrCode("https://example.com", _tempFolder, "my_code");

            filePath.Should().Be(Path.Combine(_tempFolder, "my_code.png"));
        }

        [Fact]
        public void GenerateQrCode_ShouldCreateFolderIfNotExists()
        {
            var nonExistentFolder = Path.Combine(_tempFolder, "subfolder");
            Directory.Exists(nonExistentFolder).Should().BeFalse();

            QrCodeHelper.GenerateQrCode("https://example.com", nonExistentFolder, "qr");

            Directory.Exists(nonExistentFolder).Should().BeTrue();
        }

        [Fact]
        public void GenerateQrCode_ShouldProduceNonEmptyFile()
        {
            var filePath = QrCodeHelper.GenerateQrCode("https://example.com", _tempFolder, "nonempty_qr");

            var fileInfo = new FileInfo(filePath);
            fileInfo.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public void GenerateQrCode_ShouldProduceValidPngSignature()
        {
            var filePath = QrCodeHelper.GenerateQrCode("https://example.com", _tempFolder, "png_check");

            var bytes = File.ReadAllBytes(filePath);
            // PNG magic bytes: 89 50 4E 47
            bytes[0].Should().Be(0x89);
            bytes[1].Should().Be(0x50);
            bytes[2].Should().Be(0x4E);
            bytes[3].Should().Be(0x47);
        }

        [Fact]
        public void GenerateQrCode_ShouldWorkWithDifferentUrls()
        {
            var path1 = QrCodeHelper.GenerateQrCode("https://google.com", _tempFolder, "qr1");
            var path2 = QrCodeHelper.GenerateQrCode("https://github.com", _tempFolder, "qr2");

            File.Exists(path1).Should().BeTrue();
            File.Exists(path2).Should().BeTrue();
        }
    }
}
