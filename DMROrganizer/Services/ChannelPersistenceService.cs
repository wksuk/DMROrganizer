using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using DMROrganizer.Models;
using Microsoft.VisualBasic.FileIO;
using ClosedXML.Excel;

namespace DMROrganizer.Services;

/// <summary>
/// Handles CSV/JSON import plus CSV/JSON/XLSX export routines.
/// </summary>
public sealed class ChannelPersistenceService : IChannelPersistenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public void Export(string filePath, IEnumerable<ChannelRecord> channels, ChannelExportFormat format)
    {
        switch (format)
        {
            case ChannelExportFormat.Json:
                ExportJson(filePath, channels);
                break;
            case ChannelExportFormat.Xlsx:
                ExportXlsx(filePath, channels);
                break;
            default:
                ExportCsv(filePath, channels);
                break;
        }
    }

    public IEnumerable<ChannelRecord> Import(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".csv" => ImportCsv(filePath),
            ".json" => ImportJson(filePath),
            _ => throw new InvalidOperationException("Only CSV and JSON imports are supported.")
        };
    }

    private static void ExportCsv(string filePath, IEnumerable<ChannelRecord> channels)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Alias,RxOnly,FrequencyMHz,ColorCode,Timeslot,TalkGroup,Contact,Power,Zone,Notes");
        foreach (var channel in channels)
        {
            var cells = new[]
            {
                channel.Alias,
                channel.RxOnly ? "Yes" : "No",
                channel.FrequencyMHz.ToString("F3", CultureInfo.InvariantCulture),
                channel.ColorCode.ToString(CultureInfo.InvariantCulture),
                channel.Timeslot,
                channel.TalkGroup,
                channel.Contact,
                channel.Power,
                channel.Zone,
                channel.Notes
            }.Select(EscapeCsvCell);

            builder.AppendLine(string.Join(',', cells));
        }

        File.WriteAllText(filePath, builder.ToString());
    }

    private static void ExportJson(string filePath, IEnumerable<ChannelRecord> channels)
    {
        var payload = channels.Select(channel => new SerializableChannel(channel));
        File.WriteAllText(filePath, JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static void ExportXlsx(string filePath, IEnumerable<ChannelRecord> channels)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Channels");
        var headers = new[]
        {
            "Alias", "Rx Only", "Frequency (MHz)", "Color Code", "Timeslot", "Talk Group", "Contact", "Power", "Zone", "Notes"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
            worksheet.Cell(1, i + 1).Style.Font.Bold = true;
        }

        var row = 2;
        foreach (var channel in channels)
        {
            worksheet.Cell(row, 1).Value = channel.Alias;
            worksheet.Cell(row, 2).Value = channel.RxOnly ? "Yes" : "No";
            worksheet.Cell(row, 3).Value = channel.FrequencyMHz;
            worksheet.Cell(row, 4).Value = channel.ColorCode;
            worksheet.Cell(row, 5).Value = channel.Timeslot;
            worksheet.Cell(row, 6).Value = channel.TalkGroup;
            worksheet.Cell(row, 7).Value = channel.Contact;
            worksheet.Cell(row, 8).Value = channel.Power;
            worksheet.Cell(row, 9).Value = channel.Zone;
            worksheet.Cell(row, 10).Value = channel.Notes;
            row++;
        }

        worksheet.Columns().AdjustToContents();
        workbook.SaveAs(filePath);
    }

    private static IEnumerable<ChannelRecord> ImportCsv(string filePath)
    {
        var channels = new List<ChannelRecord>();
        using var parser = new TextFieldParser(filePath)
        {
            TextFieldType = FieldType.Delimited
        };
        parser.SetDelimiters(",");
        if (!parser.EndOfData)
        {
            _ = parser.ReadFields();
        }

        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields is null || fields.Length < 10)
            {
                continue;
            }

            if (!double.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var frequency))
            {
                continue;
            }

            if (!int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var colorCode))
            {
                continue;
            }

            channels.Add(new ChannelRecord
            {
                Alias = fields[0],
                RxOnly = string.Equals(fields[1], "Yes", StringComparison.OrdinalIgnoreCase),
                FrequencyMHz = frequency,
                ColorCode = colorCode,
                Timeslot = fields[4],
                TalkGroup = fields[5],
                Contact = fields[6],
                Power = fields[7],
                Zone = fields[8],
                Notes = fields[9]
            });
        }

        return channels;
    }

    private static IEnumerable<ChannelRecord> ImportJson(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var payload = JsonSerializer.Deserialize<IEnumerable<SerializableChannel>>(json, JsonOptions) ?? Enumerable.Empty<SerializableChannel>();
        return payload.Select(item => new ChannelRecord
        {
            Alias = item.Alias ?? string.Empty,
            RxOnly = item.RxOnly,
            FrequencyMHz = item.FrequencyMHz,
            ColorCode = item.ColorCode,
            Timeslot = item.Timeslot ?? "1",
            TalkGroup = item.TalkGroup ?? string.Empty,
            Contact = item.Contact ?? string.Empty,
            Power = item.Power ?? "High",
            Zone = item.Zone ?? string.Empty,
            Notes = item.Notes ?? string.Empty
        });
    }

    private static string EscapeCsvCell(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private sealed record SerializableChannel
    {
        public SerializableChannel()
        {
        }

        public SerializableChannel(ChannelRecord channel)
        {
            Alias = channel.Alias;
            RxOnly = channel.RxOnly;
            FrequencyMHz = channel.FrequencyMHz;
            ColorCode = channel.ColorCode;
            Timeslot = channel.Timeslot;
            TalkGroup = channel.TalkGroup;
            Contact = channel.Contact;
            Power = channel.Power;
            Zone = channel.Zone;
            Notes = channel.Notes;
        }

        public string? Alias { get; set; }
        public bool RxOnly { get; set; }
        public double FrequencyMHz { get; set; }
        public int ColorCode { get; set; }
        public string? Timeslot { get; set; }
        public string? TalkGroup { get; set; }
        public string? Contact { get; set; }
        public string? Power { get; set; }
        public string? Zone { get; set; }
        public string? Notes { get; set; }
    }
}
