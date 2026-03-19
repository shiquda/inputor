using System.Globalization;
using CsvHelper;
using Inputor.App.Models;

namespace Inputor.App.Services;

public sealed class CsvExportService
{
    public string ExportToday(DashboardSnapshot snapshot)
    {
        var exportDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "inputor-exports");
        Directory.CreateDirectory(exportDirectory);

        var path = Path.Combine(exportDirectory, $"inputor-{snapshot.Today:yyyyMMdd}.csv");
        using var writer = new StreamWriter(path, false);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        csv.WriteField("date");
        csv.WriteField("app");
        csv.WriteField("count");
        csv.NextRecord();

        foreach (var stat in snapshot.AppStats.OrderByDescending(item => item.TodayCount))
        {
            csv.WriteField(snapshot.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            csv.WriteField(stat.AppName);
            csv.WriteField(stat.TodayCount);
            csv.NextRecord();
        }

        return path;
    }
}
