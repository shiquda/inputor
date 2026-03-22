using System.IO.Compression;
using System.Text.Json;
using Inputor.App.Models;
using Inputor.WinUI;

namespace Inputor.App.Services;

public sealed class BackupArchiveService
{
    private const string ManifestEntryName = "manifest.json";
    private const string SettingsEntryName = "settings.json";
    private const string StatsEntryName = "stats.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string Export(string archivePath, AppSettings settings, string statsSourcePath)
    {
        var normalizedArchivePath = Path.GetFullPath(archivePath);
        var archiveDirectory = Path.GetDirectoryName(normalizedArchivePath);
        if (string.IsNullOrWhiteSpace(archiveDirectory))
        {
            throw new InvalidOperationException("Backup archive path must have a parent directory.");
        }

        Directory.CreateDirectory(archiveDirectory);

        using var stream = new FileStream(normalizedArchivePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

        WriteEntry(archive, ManifestEntryName, JsonSerializer.Serialize(new BackupManifest
        {
            FormatVersion = 1,
            ExportedAt = DateTimeOffset.Now,
            ChannelName = AppVariant.ChannelName,
            OriginalStatisticsSourcePath = statsSourcePath
        }, JsonOptions));
        WriteEntry(archive, SettingsEntryName, JsonSerializer.Serialize(settings, JsonOptions));
        WriteEntry(archive, StatsEntryName, File.Exists(statsSourcePath) ? File.ReadAllText(statsSourcePath) : string.Empty);

        return normalizedArchivePath;
    }

    public BackupPayload Load(string archivePath)
    {
        var normalizedArchivePath = Path.GetFullPath(archivePath);
        using var stream = new FileStream(normalizedArchivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var settingsJson = ReadRequiredEntry(archive, SettingsEntryName);
        var statsJson = ReadRequiredEntry(archive, StatsEntryName);
        var manifestJson = ReadOptionalEntry(archive, ManifestEntryName);

        var settings = JsonSerializer.Deserialize<AppSettings>(settingsJson, JsonOptions)
            ?? throw new InvalidDataException("Backup archive settings payload is invalid.");
        var manifest = string.IsNullOrWhiteSpace(manifestJson)
            ? null
            : JsonSerializer.Deserialize<BackupManifest>(manifestJson, JsonOptions);

        return new BackupPayload
        {
            Settings = settings,
            StatsJson = statsJson,
            Manifest = manifest
        };
    }

    private static string ReadRequiredEntry(ZipArchive archive, string entryName)
    {
        return ReadOptionalEntry(archive, entryName)
            ?? throw new InvalidDataException($"Backup archive is missing required entry '{entryName}'.");
    }

    private static string? ReadOptionalEntry(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName);
        if (entry is null)
        {
            return null;
        }

        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    private static void WriteEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }

    public sealed class BackupPayload
    {
        public required AppSettings Settings { get; init; }
        public required string StatsJson { get; init; }
        public BackupManifest? Manifest { get; init; }
    }

    public sealed class BackupManifest
    {
        public int FormatVersion { get; init; }
        public DateTimeOffset ExportedAt { get; init; }
        public string ChannelName { get; init; } = string.Empty;
        public string OriginalStatisticsSourcePath { get; init; } = string.Empty;
    }
}
