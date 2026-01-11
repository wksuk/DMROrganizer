namespace DMROrganizer.ViewModels;

/// <summary>
/// Small helper that pairs filter display text with the actual value.
/// </summary>
public sealed record FilterOption<T>(string Label, T? Value);
