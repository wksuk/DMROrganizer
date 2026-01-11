using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using DMROrganizer.Helpers;
using DMROrganizer.Models;
using DMROrganizer.Services;

namespace DMROrganizer.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IChannelPersistenceService _persistenceService;
    private readonly ICollectionView _channelsView;
    private readonly string _autosaveFilePath;

    private string _searchText = string.Empty;
    private string? _timeslotFilter;
    private int? _colorCodeFilter;
    private string? _powerFilter;
    private bool? _rxOnlyFilter;
    private string? _zoneFilter;
    private string _selectedSortField = "Frequency";
    private bool _isSortDescending;
    private bool _isDarkTheme;
    private string _newZoneName = string.Empty;
    private string? _selectedZone;
    private ChannelRecord? _selectedChannel;

    public MainViewModel(IChannelPersistenceService persistenceService)
    {
        _persistenceService = persistenceService;
        _autosaveFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DMROrganizer", "autosave.csv");
        Channels = new ObservableCollection<ChannelRecord>();
        Channels.CollectionChanged += ChannelsCollectionChanged;
        Zones = new ObservableCollection<string>();
        NewChannel = new ChannelFormModel();
        _channelsView = CollectionViewSource.GetDefaultView(Channels);
        _channelsView.Filter = FilterChannel;
        _channelsView.SortDescriptions.Add(new SortDescription(nameof(ChannelRecord.FrequencyMHz), ListSortDirection.Ascending));
        _channelsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ChannelRecord.Zone)));

        SaveChannelCommand = new RelayCommand(_ => SaveChannel());
        NewChannelCommand = new RelayCommand(_ => StartNewChannel());
        DeleteChannelCommand = new RelayCommand(_ => DeleteChannel(), _ => SelectedChannel is not null);
        ExportCommand = new RelayCommand(_ => ExportChannels());
        ImportCommand = new RelayCommand(_ => ImportChannels());
        ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());
        AddZoneCommand = new RelayCommand(_ => AddZone());
        DeleteZoneCommand = new RelayCommand(_ => DeleteZone(), _ => !string.IsNullOrWhiteSpace(SelectedZone));
        ShowAboutCommand = new RelayCommand(_ => ShowAbout());

        if (!TryLoadAutoSave())
        {
            SeedSampleData();
        }

        ApplyThemeResources();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ChannelFormModel NewChannel { get; }

    public ObservableCollection<ChannelRecord> Channels { get; }

    public ObservableCollection<string> Zones { get; }

    public IEnumerable<string> TimeslotOptions { get; } = new[] { "1", "2", "XPT" };

    public IEnumerable<FilterOption<string?>> TimeslotFilterOptions { get; } = new[]
    {
        new FilterOption<string?>("Any", null),
        new FilterOption<string?>("Timeslot 1", "1"),
        new FilterOption<string?>("Timeslot 2", "2"),
        new FilterOption<string?>("XPT", "XPT")
    };

    public IEnumerable<string> PowerOptions { get; } = new[] { "High", "Low" };

    public IEnumerable<FilterOption<string?>> PowerFilterOptions { get; } = new[]
    {
        new FilterOption<string?>("Any", null),
        new FilterOption<string?>("High", "High"),
        new FilterOption<string?>("Low", "Low")
    };

    public IEnumerable<int> ColorCodes { get; } = Enumerable.Range(0, 16).ToList();

    public IEnumerable<FilterOption<bool?>> RxOnlyOptions { get; } = new[]
    {
        new FilterOption<bool?>("Any", null),
        new FilterOption<bool?>("Rx Only", true),
        new FilterOption<bool?>("Tx/Rx", false)
    };

    public IEnumerable<FilterOption<int?>> ColorCodeFilterChoices { get; } = BuildColorCodeFilterOptions();

    public ObservableCollection<FilterOption<string?>> ZoneFilterChoices { get; }
        = new(new[] { new FilterOption<string?>("All Zones", null) });

    public IEnumerable<string> SortFields { get; } = new[]
    {
        "Alias",
        "Frequency",
        "ColorCode",
        "Timeslot",
        "Power",
        "Zone"
    };

    public ICollectionView FilteredChannels => _channelsView;

    public RelayCommand SaveChannelCommand { get; }

    public RelayCommand NewChannelCommand { get; }

    public RelayCommand DeleteChannelCommand { get; }

    public RelayCommand ExportCommand { get; }

    public RelayCommand ImportCommand { get; }

    public RelayCommand ToggleThemeCommand { get; }

    public RelayCommand AddZoneCommand { get; }

    public RelayCommand DeleteZoneCommand { get; }

    public RelayCommand ShowAboutCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value)
            {
                return;
            }

            _searchText = value;
            OnPropertyChanged(nameof(SearchText));
            _channelsView.Refresh();
        }
    }

    public string? TimeslotFilter
    {
        get => _timeslotFilter;
        set
        {
            if (_timeslotFilter == value)
            {
                return;
            }

            _timeslotFilter = value;
            OnPropertyChanged(nameof(TimeslotFilter));
            _channelsView.Refresh();
        }
    }

    public int? ColorCodeFilter
    {
        get => _colorCodeFilter;
        set
        {
            if (_colorCodeFilter == value)
            {
                return;
            }

            _colorCodeFilter = value;
            OnPropertyChanged(nameof(ColorCodeFilter));
            _channelsView.Refresh();
        }
    }

    public string? PowerFilter
    {
        get => _powerFilter;
        set
        {
            if (_powerFilter == value)
            {
                return;
            }

            _powerFilter = value;
            OnPropertyChanged(nameof(PowerFilter));
            _channelsView.Refresh();
        }
    }

    public bool? RxOnlyFilter
    {
        get => _rxOnlyFilter;
        set
        {
            if (_rxOnlyFilter == value)
            {
                return;
            }

            _rxOnlyFilter = value;
            OnPropertyChanged(nameof(RxOnlyFilter));
            _channelsView.Refresh();
        }
    }

    public string? ZoneFilter
    {
        get => _zoneFilter;
        set
        {
            if (_zoneFilter == value)
            {
                return;
            }

            _zoneFilter = value;
            OnPropertyChanged(nameof(ZoneFilter));
            _channelsView.Refresh();
        }
    }

    public string SelectedSortField
    {
        get => _selectedSortField;
        set
        {
            if (_selectedSortField == value)
            {
                return;
            }

            _selectedSortField = value;
            OnPropertyChanged(nameof(SelectedSortField));
            ApplySorting();
        }
    }

    public bool IsSortDescending
    {
        get => _isSortDescending;
        set
        {
            if (_isSortDescending == value)
            {
                return;
            }

            _isSortDescending = value;
            OnPropertyChanged(nameof(IsSortDescending));
            ApplySorting();
        }
    }

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        private set
        {
            if (_isDarkTheme == value)
            {
                return;
            }

            _isDarkTheme = value;
            OnPropertyChanged(nameof(IsDarkTheme));
        }
    }

    public string NewZoneName
    {
        get => _newZoneName;
        set
        {
            if (_newZoneName == value)
            {
                return;
            }

            _newZoneName = value;
            OnPropertyChanged(nameof(NewZoneName));
        }
    }

    public string? SelectedZone
    {
        get => _selectedZone;
        set
        {
            if (_selectedZone == value)
            {
                return;
            }

            _selectedZone = value;
            OnPropertyChanged(nameof(SelectedZone));
            DeleteZoneCommand.RaiseCanExecuteChanged();
        }
    }

    public ChannelRecord? SelectedChannel
    {
        get => _selectedChannel;
        set
        {
            if (ReferenceEquals(_selectedChannel, value))
            {
                return;
            }

            _selectedChannel = value;
            OnPropertyChanged(nameof(SelectedChannel));
            DeleteChannelCommand.RaiseCanExecuteChanged();
            if (_selectedChannel is null)
            {
                ResetForm();
            }
            else
            {
                LoadFormFromChannel(_selectedChannel);
            }
        }
    }

    private static IEnumerable<FilterOption<int?>> BuildColorCodeFilterOptions()
    {
        var list = new List<FilterOption<int?>> { new("Any", null) };
        for (var i = 0; i <= 15; i++)
        {
            list.Add(new FilterOption<int?>("CC " + i, i));
        }

        return list;
    }

    private void SaveChannel()
    {
        if (!NewChannel.TryBuildRecord(out var record, out var errorMessage))
        {
            MessageBox.Show(errorMessage, "Invalid Channel", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (SelectedChannel is null)
        {
            if (IsDuplicate(record, null))
            {
                MessageBox.Show("Duplicate detected (Frequency + Color Code + Timeslot must be unique).", "Duplicate Channel", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Channels.Add(record);
            EnsureZone(record.Zone);
            StartNewChannel();
        }
        else
        {
            if (IsDuplicate(record, SelectedChannel.Id))
            {
                MessageBox.Show("Duplicate detected (Frequency + Color Code + Timeslot must be unique).", "Duplicate Channel", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ApplyRecordToChannel(SelectedChannel, record);
            EnsureZone(record.Zone);
            _channelsView.Refresh();
        }

        AutoSave();
    }

    private void StartNewChannel()
    {
        SelectedChannel = null;
        ResetForm();
    }

    private void ResetForm()
    {
        NewChannel.Reset();
        NewChannel.Zone = Zones.FirstOrDefault() ?? string.Empty;
    }

    private void LoadFormFromChannel(ChannelRecord channel)
    {
        NewChannel.Alias = channel.Alias;
        NewChannel.FrequencyText = channel.FrequencyMHz.ToString("F3");
        NewChannel.RxOnly = channel.RxOnly;
        NewChannel.ColorCode = channel.ColorCode;
        NewChannel.Timeslot = channel.Timeslot;
        NewChannel.TalkGroup = channel.TalkGroup;
        NewChannel.Contact = channel.Contact;
        NewChannel.Power = channel.Power;
        NewChannel.Zone = channel.Zone;
        NewChannel.Notes = channel.Notes;
    }

    private static void ApplyRecordToChannel(ChannelRecord target, ChannelRecord source)
    {
        target.Alias = source.Alias;
        target.RxOnly = source.RxOnly;
        target.FrequencyMHz = source.FrequencyMHz;
        target.ColorCode = source.ColorCode;
        target.Timeslot = source.Timeslot;
        target.TalkGroup = source.TalkGroup;
        target.Contact = source.Contact;
        target.Power = source.Power;
        target.Zone = source.Zone;
        target.Notes = source.Notes;
    }

    private bool FilterChannel(object obj)
    {
        if (obj is not ChannelRecord channel)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            if (!channel.SearchBlob.Contains(SearchText.Trim().ToLowerInvariant()))
            {
                return false;
            }
        }

        if (!string.IsNullOrEmpty(TimeslotFilter) && !string.Equals(channel.Timeslot, TimeslotFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (ColorCodeFilter.HasValue && channel.ColorCode != ColorCodeFilter.Value)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(PowerFilter) && !string.Equals(channel.Power, PowerFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (RxOnlyFilter.HasValue && channel.RxOnly != RxOnlyFilter.Value)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(ZoneFilter) && !string.Equals(channel.Zone, ZoneFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private bool IsDuplicate(ChannelRecord candidate, Guid? ignoreId) => Channels.Any(channel => channel.Id != ignoreId && Math.Abs(channel.FrequencyMHz - candidate.FrequencyMHz) < 0.0001 && channel.ColorCode == candidate.ColorCode && string.Equals(channel.Timeslot, candidate.Timeslot, StringComparison.OrdinalIgnoreCase));

    private void ApplySorting()
    {
        _channelsView.SortDescriptions.Clear();
        var direction = IsSortDescending ? ListSortDirection.Descending : ListSortDirection.Ascending;
        var sortDescription = SelectedSortField switch
        {
            "Alias" => new SortDescription(nameof(ChannelRecord.Alias), direction),
            "ColorCode" => new SortDescription(nameof(ChannelRecord.ColorCode), direction),
            "Timeslot" => new SortDescription(nameof(ChannelRecord.Timeslot), direction),
            "Power" => new SortDescription(nameof(ChannelRecord.Power), direction),
            "Zone" => new SortDescription(nameof(ChannelRecord.Zone), direction),
            _ => new SortDescription(nameof(ChannelRecord.FrequencyMHz), direction)
        };

        _channelsView.SortDescriptions.Add(sortDescription);
    }

    private void ExportChannels()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv|JSON (*.json)|*.json|Excel Workbook (*.xlsx)|*.xlsx",
            FileName = "dmr-channels"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var format = dialog.FilterIndex switch
            {
                1 => ChannelExportFormat.Csv,
                2 => ChannelExportFormat.Json,
                3 => ChannelExportFormat.Xlsx,
                _ => ChannelExportFormat.Csv
            };

            _persistenceService.Export(dialog.FileName, Channels, format);
            MessageBox.Show("Channels exported successfully.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed: {ex.Message}", "Export", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportChannels()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "CSV (*.csv)|*.csv|JSON (*.json)|*.json"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var imported = _persistenceService.Import(dialog.FileName).ToList();
            if (!imported.Any())
            {
                MessageBox.Show("No channels found in file.", "Import", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var added = 0;
            foreach (var channel in imported)
            {
                if (IsDuplicate(channel, null))
                {
                    continue;
                }

                Channels.Add(channel);
                EnsureZone(channel.Zone);
                added++;
            }

            _channelsView.Refresh();
            AutoSave();
            MessageBox.Show($"Imported {added} channel(s).", "Import", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Import failed: {ex.Message}", "Import", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        ApplyThemeResources();
    }

    private void ApplyThemeResources()
    {
        var resources = Application.Current.Resources;
        if (IsDarkTheme)
        {
            resources["AppBackgroundBrush"] = System.Windows.Media.Brushes.Black;
            resources["CardBackgroundBrush"] = System.Windows.Media.Brushes.DimGray;
            resources["AppForegroundBrush"] = System.Windows.Media.Brushes.WhiteSmoke;
        }
        else
        {
            resources["AppBackgroundBrush"] = System.Windows.Media.Brushes.White;
            resources["CardBackgroundBrush"] = System.Windows.Media.Brushes.WhiteSmoke;
            resources["AppForegroundBrush"] = System.Windows.Media.Brushes.Black;
        }
    }

    private void AddZone()
    {
        if (string.IsNullOrWhiteSpace(NewZoneName))
        {
            return;
        }

        var zone = NewZoneName.Trim();
        EnsureZone(zone);
        NewChannel.Zone = zone;
        NewZoneName = string.Empty;
    }

    private void DeleteChannel()
    {
        if (SelectedChannel is null)
        {
            return;
        }

        Channels.Remove(SelectedChannel);
        SelectedChannel = null;
        _channelsView.Refresh();
        AutoSave();
    }

    private void DeleteZone()
    {
        if (string.IsNullOrWhiteSpace(SelectedZone))
        {
            return;
        }

        var zone = SelectedZone;
        if (!Zones.Contains(zone))
        {
            return;
        }

        Zones.Remove(zone);
        var filterOption = ZoneFilterChoices.FirstOrDefault(z => string.Equals(z.Value, zone, StringComparison.OrdinalIgnoreCase));
        if (filterOption is not null)
        {
            ZoneFilterChoices.Remove(filterOption);
        }

        foreach (var channel in Channels.Where(channel => string.Equals(channel.Zone, zone, StringComparison.OrdinalIgnoreCase)))
        {
            channel.Zone = string.Empty;
        }

        if (string.Equals(NewChannel.Zone, zone, StringComparison.OrdinalIgnoreCase))
        {
            NewChannel.Zone = Zones.FirstOrDefault() ?? string.Empty;
        }

        if (string.Equals(ZoneFilter, zone, StringComparison.OrdinalIgnoreCase))
        {
            ZoneFilter = null;
        }

        SelectedZone = null;
        _channelsView.Refresh();
        AutoSave();
    }

    private void EnsureZone(string zone)
    {
        if (string.IsNullOrWhiteSpace(zone))
        {
            return;
        }

        if (Zones.Contains(zone))
        {
            return;
        }

        var insertIndex = 0;
        while (insertIndex < Zones.Count && string.Compare(Zones[insertIndex], zone, StringComparison.OrdinalIgnoreCase) < 0)
        {
            insertIndex++;
        }

        Zones.Insert(insertIndex, zone);
        ZoneFilterChoices.Insert(insertIndex + 1, new FilterOption<string?>(zone, zone));
    }

    private bool TryLoadAutoSave()
    {
        try
        {
            if (!File.Exists(_autosaveFilePath))
            {
                return false;
            }

            var imported = _persistenceService.Import(_autosaveFilePath).ToList();
            if (!imported.Any())
            {
                return false;
            }

            foreach (var channel in imported)
            {
                Channels.Add(channel);
                EnsureZone(channel.Zone);
            }

            if (!string.IsNullOrEmpty(Zones.FirstOrDefault()))
            {
                NewChannel.Zone = Zones[0];
            }

            _channelsView.Refresh();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void AutoSave()
    {
        try
        {
            var directory = Path.GetDirectoryName(_autosaveFilePath);
            if (string.IsNullOrEmpty(directory))
            {
                // Log or skip if path invalid
                return;
            }
            Directory.CreateDirectory(directory);

            _persistenceService.Export(_autosaveFilePath, Channels, ChannelExportFormat.Csv);
        }
        catch (Exception ex)
        {
            // Silent fail with optional logging (e.g., to console or file)
            System.Diagnostics.Debug.WriteLine($"AutoSave failed: {ex.Message}");
        }
    }

    private void SeedSampleData()
    {
        var sample = new List<ChannelRecord>
        {
            new()
            {
                Alias = "City Center",
                FrequencyMHz = 443.600,
                ColorCode = 1,
                Timeslot = "1",
                TalkGroup = "Local",
                Contact = "TG 91",
                Zone = "City Repeaters",
                RxOnly = false,
                Power = "High",
                Notes = "Main repeater downtown."
            },
            new()
            {
                Alias = "Highway Link",
                FrequencyMHz = 442.725,
                ColorCode = 4,
                Timeslot = "XPT",
                TalkGroup = "Regional",
                Contact = "TG 3000",
                Zone = "Highways",
                RxOnly = false,
                Power = "High",
                Notes = "Highway communication."
            }
        };

        foreach (var channel in sample)
        {
            Channels.Add(channel);
            EnsureZone(channel.Zone);
        }

        if (!string.IsNullOrEmpty(Zones.FirstOrDefault()))
        {
            NewChannel.Zone = Zones[0];
        }

        _channelsView.Refresh();
        AutoSave();
    }

    private void ChannelsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        AutoSave();
    }

    private void ShowAbout()
    {
        MessageBox.Show("DMR Organizer\nAuthor: WKSUK\nVersion 1.0\nORCID ID: 0000-0000-0000-0000", "About", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
