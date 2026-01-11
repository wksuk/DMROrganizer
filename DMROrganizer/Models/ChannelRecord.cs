using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DMROrganizer.Models;

/// <summary>
/// Represents a single DMR channel entry that is displayed inside the organizer.
/// </summary>
public sealed class ChannelRecord : INotifyPropertyChanged
{
    private string _alias = string.Empty;
    private bool _rxOnly;
    private double _frequencyMHz;
    private int _colorCode;
    private string _timeslot = "1";
    private string _talkGroup = string.Empty;
    private string _contact = string.Empty;
    private string _power = "High";
    private string _zone = string.Empty;
    private string _notes = string.Empty;

    public Guid Id { get; } = Guid.NewGuid();

    public string Alias
    {
        get => _alias;
        set => SetProperty(ref _alias, value);
    }

    public bool RxOnly
    {
        get => _rxOnly;
        set => SetProperty(ref _rxOnly, value);
    }

    /// <summary>
    /// Operating frequency expressed in MHz.
    /// </summary>
    public double FrequencyMHz
    {
        get => _frequencyMHz;
        set => SetProperty(ref _frequencyMHz, value);
    }

    public int ColorCode
    {
        get => _colorCode;
        set => SetProperty(ref _colorCode, value);
    }

    public string Timeslot
    {
        get => _timeslot;
        set => SetProperty(ref _timeslot, value);
    }

    public string TalkGroup
    {
        get => _talkGroup;
        set => SetProperty(ref _talkGroup, value);
    }

    public string Contact
    {
        get => _contact;
        set => SetProperty(ref _contact, value);
    }

    public string Power
    {
        get => _power;
        set => SetProperty(ref _power, value);
    }

    public string Zone
    {
        get => _zone;
        set => SetProperty(ref _zone, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    /// <summary>
    /// Cached string that is used to run search queries quickly.
    /// </summary>
    public string SearchBlob => string.Join("|", Alias, FrequencyMHz.ToString("F3"), ColorCode, Timeslot, TalkGroup, Contact, Zone, Power, RxOnly ? "RxOnly" : "TxRx", Notes).ToLowerInvariant();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        if (propertyName is not nameof(SearchBlob))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SearchBlob)));
        }
    }
}
