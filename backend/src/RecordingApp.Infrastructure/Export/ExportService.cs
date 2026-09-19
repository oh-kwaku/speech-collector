using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace RecordingApp.Infrastructure.Export;

public record ExportRow(
    Guid PhotoId,
    Guid SessionId,
    Guid UserId,
    string SpeakerId,
    string? Annotation,
    DateTimeOffset CreatedAt,
    string SpeakerGender,
    int SpeakerAge);

public interface IExportService
{
    Task<byte[]> ExportCsvAsync(CancellationToken ct = default);
    Task<byte[]> ExportXlsxAsync(CancellationToken ct = default);
}

public class ExportService(RecordingAppDbContext db) : IExportService
{
    private static readonly string[] Headers =
    [
        "photoId", "sessionId", "userId", "speakerId", "annotation",
        "created_at", "speaker_gender", "speaker_age",
    ];

    public async Task<byte[]> ExportCsvAsync(CancellationToken ct = default)
    {
        var rows = await QueryRowsAsync(ct);
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', Headers));
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(',', new[]
            {
                r.PhotoId.ToString(),
                r.SessionId.ToString(),
                r.UserId.ToString(),
                CsvEscape(r.SpeakerId),
                CsvEscape(r.Annotation ?? ""),
                r.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
                r.SpeakerGender,
                r.SpeakerAge.ToString(CultureInfo.InvariantCulture),
            }));
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public async Task<byte[]> ExportXlsxAsync(CancellationToken ct = default)
    {
        var rows = await QueryRowsAsync(ct);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Recordings");
        for (var i = 0; i < Headers.Length; i++)
            sheet.Cell(1, i + 1).Value = Headers[i];

        var rowIndex = 2;
        foreach (var r in rows)
        {
            sheet.Cell(rowIndex, 1).Value = r.PhotoId.ToString();
            sheet.Cell(rowIndex, 2).Value = r.SessionId.ToString();
            sheet.Cell(rowIndex, 3).Value = r.UserId.ToString();
            sheet.Cell(rowIndex, 4).Value = r.SpeakerId;
            sheet.Cell(rowIndex, 5).Value = r.Annotation ?? "";
            sheet.Cell(rowIndex, 6).Value = r.CreatedAt.UtcDateTime;
            sheet.Cell(rowIndex, 7).Value = r.SpeakerGender;
            sheet.Cell(rowIndex, 8).Value = r.SpeakerAge;
            rowIndex++;
        }
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private async Task<List<ExportRow>> QueryRowsAsync(CancellationToken ct) =>
        await db.Recordings
            .Where(r => r.IsConfirmed)
            .Include(r => r.Annotation)
            .Include(r => r.Session).ThenInclude(s => s!.Speaker)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new ExportRow(
                r.PhotoId,
                r.SessionId,
                r.CreatedByUserId,
                r.SpeakerId,
                r.Annotation != null ? r.Annotation.Text : null,
                r.CreatedAt,
                r.Session!.Speaker!.Gender.ToString(),
                r.Session.Speaker.AgeYears))
            .ToListAsync(ct);

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
