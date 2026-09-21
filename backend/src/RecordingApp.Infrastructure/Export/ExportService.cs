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

public record IrrExportRow(
    Guid RecordingId,
    Guid PhotoId,
    Guid SessionId,
    string SpeakerId,
    Guid AnnotatorUserId,
    string Annotation,
    DateTimeOffset CreatedAt);

public interface IExportService
{
    Task<byte[]> ExportCsvAsync(CancellationToken ct = default);
    Task<byte[]> ExportXlsxAsync(CancellationToken ct = default);
    Task<byte[]> ExportIrrCsvAsync(CancellationToken ct = default);
    Task<byte[]> ExportIrrXlsxAsync(CancellationToken ct = default);
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

    // IRR: a recording may now carry several independent annotations (one per
    // annotator). The main export keeps its documented one-row-per-recording
    // schema, so it uses the recording's CanonicalAnnotation for the
    // `annotation` column - the first annotation submitted by default, or
    // whichever one an Admin/adjudicator repointed it to after reviewing
    // rater disagreement (see AnnotationsController.SetCanonical). Use
    // ExportIrrCsvAsync/ExportIrrXlsxAsync below to get every annotator's
    // text per recording for computing agreement.
    private async Task<List<ExportRow>> QueryRowsAsync(CancellationToken ct) =>
        await db.Recordings
            .Where(r => r.IsConfirmed)
            .Include(r => r.CanonicalAnnotation)
            .Include(r => r.Session).ThenInclude(s => s!.Speaker)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new ExportRow(
                r.PhotoId,
                r.SessionId,
                r.CreatedByUserId,
                r.SpeakerId,
                r.CanonicalAnnotation != null ? r.CanonicalAnnotation.Text : null,
                r.CreatedAt,
                r.Session!.Speaker!.Gender.ToString(),
                r.Session.Speaker.AgeYears))
            .ToListAsync(ct);

    private static readonly string[] IrrHeaders =
    [
        "recordingId", "photoId", "sessionId", "speakerId", "annotatorUserId",
        "annotation", "created_at",
    ];

    public async Task<byte[]> ExportIrrCsvAsync(CancellationToken ct = default)
    {
        var rows = await QueryIrrRowsAsync(ct);
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', IrrHeaders));
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(',', new[]
            {
                r.RecordingId.ToString(),
                r.PhotoId.ToString(),
                r.SessionId.ToString(),
                CsvEscape(r.SpeakerId),
                r.AnnotatorUserId.ToString(),
                CsvEscape(r.Annotation),
                r.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
            }));
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public async Task<byte[]> ExportIrrXlsxAsync(CancellationToken ct = default)
    {
        var rows = await QueryIrrRowsAsync(ct);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("IRR");
        for (var i = 0; i < IrrHeaders.Length; i++)
            sheet.Cell(1, i + 1).Value = IrrHeaders[i];

        var rowIndex = 2;
        foreach (var r in rows)
        {
            sheet.Cell(rowIndex, 1).Value = r.RecordingId.ToString();
            sheet.Cell(rowIndex, 2).Value = r.PhotoId.ToString();
            sheet.Cell(rowIndex, 3).Value = r.SessionId.ToString();
            sheet.Cell(rowIndex, 4).Value = r.SpeakerId;
            sheet.Cell(rowIndex, 5).Value = r.AnnotatorUserId.ToString();
            sheet.Cell(rowIndex, 6).Value = r.Annotation;
            sheet.Cell(rowIndex, 7).Value = r.CreatedAt.UtcDateTime;
            rowIndex++;
        }
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // One row per (recording, annotator) pair, for every recording that has
    // at least 2 annotations - i.e. only the recordings actually usable for
    // an IRR/agreement computation.
    private async Task<List<IrrExportRow>> QueryIrrRowsAsync(CancellationToken ct) =>
        await db.Annotations
            .Where(a => a.Recording!.IsConfirmed && a.Recording.Annotations.Count > 1)
            .OrderBy(a => a.RecordingId).ThenBy(a => a.CreatedAt)
            .Select(a => new IrrExportRow(
                a.RecordingId,
                a.Recording!.PhotoId,
                a.Recording.SessionId,
                a.Recording.SpeakerId,
                a.AnnotatorUserId,
                a.Text,
                a.CreatedAt))
            .ToListAsync(ct);

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
