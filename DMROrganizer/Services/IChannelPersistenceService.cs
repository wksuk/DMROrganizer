using System.Collections.Generic;
using DMROrganizer.Models;

namespace DMROrganizer.Services;

/// <summary>
/// Describes persistence operations for importing and exporting channel data.
/// </summary>
public interface IChannelPersistenceService
{
    void Export(string filePath, IEnumerable<ChannelRecord> channels, ChannelExportFormat format);

    IEnumerable<ChannelRecord> Import(string filePath);
}

public enum ChannelExportFormat
{
    Csv,
    Json,
    Xlsx
}
