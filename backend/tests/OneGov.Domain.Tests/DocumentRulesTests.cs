using System.Text;
using OneGov.Domain.Documents;

namespace OneGov.Domain.Tests;

public class DocumentRulesTests
{
    private static byte[] Bytes(string text) => Encoding.ASCII.GetBytes(text);

    [Fact]
    public void Pdf_png_and_jpeg_are_recognised_by_their_signature()
    {
        Assert.Equal(new DetectedFile("application/pdf", ".pdf"), DocumentRules.Detect(Bytes("%PDF-1.7 rest of file")));
        Assert.Equal(
            new DetectedFile("image/png", ".png"),
            DocumentRules.Detect([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00]));
        Assert.Equal(new DetectedFile("image/jpeg", ".jpg"), DocumentRules.Detect([0xFF, 0xD8, 0xFF, 0xE0, 0x00]));
    }

    [Theory]
    [InlineData("MZ\u0090\u0000 a Windows program renamed to .pdf")]
    [InlineData("just some text")]
    [InlineData("   %PDF-1.4 signature not at the start")]
    [InlineData("<html><script>alert(1)</script></html>")]
    [InlineData("")]
    public void Anything_else_is_refused(string content) =>
        Assert.Null(DocumentRules.Detect(Bytes(content)));

    [Theory]
    [InlineData("../../evil.pdf", ".pdf", "evil.pdf")]
    [InlineData("C:\\Users\\me\\Tax Return.pdf", ".pdf", "Tax Return.pdf")]
    [InlineData("a<b>c:d|e?f*g\"h.pdf", ".pdf", "abcdefgh.pdf")]
    [InlineData("photo.exe", ".png", "photo.png")]
    [InlineData("report.final.pdf", ".pdf", "report.final.pdf")]
    [InlineData("two   spaces.pdf", ".pdf", "two spaces.pdf")]
    [InlineData("", ".pdf", "document.pdf")]
    [InlineData(null, ".jpg", "document.jpg")]
    [InlineData("...", ".pdf", "document.pdf")]
    public void Names_are_made_safe_and_get_the_real_extension(string? clientName, string extension, string expected) =>
        Assert.Equal(expected, DocumentRules.DisplayName(clientName, extension));

    [Fact]
    public void Control_characters_are_removed()
    {
        Assert.Equal("ab.pdf", DocumentRules.DisplayName("a\u0000b\r\n.pdf", ".pdf"));
    }

    [Fact]
    public void Amharic_names_are_kept()
    {
        Assert.Equal("የገቢ ማስረጃ.pdf", DocumentRules.DisplayName("የገቢ ማስረጃ.pdf", ".pdf"));
    }

    [Fact]
    public void Very_long_names_are_shortened()
    {
        var name = DocumentRules.DisplayName(new string('a', 500) + ".pdf", ".pdf");

        Assert.Equal(DocumentRules.MaxNameLength + ".pdf".Length, name.Length);
        Assert.EndsWith(".pdf", name);
    }
}

public class ApplicationDocumentTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 8, 0, 0, TimeSpan.Zero);

    private static ApplicationDocument Create(long size = 10) =>
        ApplicationDocument.Create(Guid.NewGuid(), Guid.NewGuid(), "a.pdf", "application/pdf", size, "abc", new string('a', 32), Now);

    [Fact]
    public void A_new_document_is_not_scanned_and_not_deleted()
    {
        var document = Create();

        Assert.Equal("not_scanned", document.ScanStatus);
        Assert.Null(document.DeletedAt);
    }

    [Fact]
    public void Deleting_twice_keeps_the_first_time()
    {
        var document = Create();

        document.Delete(Now.AddMinutes(1));
        document.Delete(Now.AddMinutes(9));

        Assert.Equal(Now.AddMinutes(1), document.DeletedAt);
    }

    [Fact]
    public void An_empty_file_cannot_be_recorded() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(size: 0));
}
