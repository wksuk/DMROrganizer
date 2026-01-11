using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using DMROrganizer.Models;

namespace DMROrganizer.ViewModels;

/// <summary>
/// Maintains the state of the channel entry form and performs validation.
/// </summary>
public sealed class ChannelFormModel : INotifyPropertyChanged, INotifyDataErrorInfo
{
    private readonly Dictionary<string, List<string>> _errors = new();

    private string _alias = string.Empty;
    private string _frequencyText = string.Empty;
    private bool _rxOnly;
    private int? _colorCode = 1;
    private string _timeslot = "1";
    private string _talkGroup = string.Empty;
    private string _contact = string.Empty;
    private string _power = "High";
    private string _zone = string.Empty;
    private string _notes = string.Empty;

    public string Alias
    {
        get => _alias;
        set => SetProperty(ref _alias, value, ValidateAlias);
    }

    public string FrequencyText
    {
        get => _frequencyText;
        set => SetProperty(ref _frequencyText, value, ValidateFrequency);
    }

    public bool RxOnly
    {
        get => _rxOnly;
        set => SetProperty(ref _rxOnly, value);
    }

    public int? ColorCode
    {
        get => _colorCode;
        set => SetProperty(ref _colorCode, value, ValidateColorCode);
    }

    public string Timeslot
    {
        get => _timeslot;
        set => SetProperty(ref _timeslot, value, ValidateTimeslot);
    }

    public string TalkGroup
    {
        get => _talkGroup;
        set => SetProperty(ref _talkGroup, value, ValidateTalkGroup);
    }

    public string Contact
    {
        get => _contact;
        set => SetProperty(ref _contact, value, ValidateContact);
    }

    public string Power
    {
        get => _power;
        set => SetProperty(ref _power, value, ValidatePower);
    }

    public string Zone
    {
        get => _zone;
        set => SetProperty(ref _zone, value, ValidateZone);
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public bool HasErrors => _errors.Count > 0;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public IEnumerable GetErrors(string? propertyName)
    {
        if (propertyName is null)
        {
            return Array.Empty<string>();
        }

        return _errors.TryGetValue(propertyName, out var value) ? value : Array.Empty<string>();
    }

    /// <summary>
    /// Verifies all fields and reports validation errors back to WPF bindings.
    /// </summary>
    public bool ValidateAll()
    {
        ValidateAlias();
        ValidateFrequency();
        ValidateColorCode();
        ValidateTimeslot();
        ValidateTalkGroup();
        ValidateContact();
        ValidatePower();
        ValidateZone();
        return !HasErrors;
    }

    /// <summary>
    /// Resets the form to safe defaults after saving.
    /// </summary>
    public void Reset()
    {
        Alias = string.Empty;
        FrequencyText = string.Empty;
        RxOnly = false;
        ColorCode = 1;
        Timeslot = "1";
        TalkGroup = string.Empty;
        Contact = string.Empty;
        Power = "High";
        Zone = string.Empty;
        Notes = string.Empty;
    }

    public bool TryBuildRecord(out ChannelRecord record, out string errorMessage)
    {
        record = null!;
        errorMessage = string.Empty;

        if (!ValidateAll())
        {
            errorMessage = "Please fix validation errors before saving.";
            return false;
        }

        if (!TryGetFrequency(out var frequency))
        {
            errorMessage = "Frequency must be between 30 and 1300 MHz.";
            return false;
        }

        if (ColorCode is null)
        {
            errorMessage = "Color code is required.";
            return false;
        }

        record = new ChannelRecord
        {
            Alias = Alias.Trim(),
            FrequencyMHz = frequency,
            RxOnly = RxOnly,
            ColorCode = ColorCode.Value,
            Timeslot = Timeslot,
            TalkGroup = TalkGroup.Trim(),
            Contact = Contact.Trim(),
            Power = Power,
            Zone = Zone.Trim(),
            Notes = Notes.Trim()
        };

        return true;
    }

    public bool TryGetFrequency(out double frequency)
    {
        if (double.TryParse(FrequencyText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ||
            double.TryParse(FrequencyText, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed))
        {
            frequency = parsed;
            return true;
        }

        frequency = 0;
        return false;
    }

    private void SetProperty<T>(ref T field, T value, Action? validationCallback = null, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        validationCallback?.Invoke();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void ValidateAlias()
    {
        SetOrClearError(nameof(Alias), string.IsNullOrWhiteSpace(Alias) ? "Alias is required." : null);
    }

    private void ValidateFrequency()
    {
        if (!TryGetFrequency(out var frequency))
        {
            SetOrClearError(nameof(FrequencyText), "Enter a valid numeric frequency (MHz).");
            return;
        }

        if (frequency is < 30 or > 1300)
        {
            SetOrClearError(nameof(FrequencyText), "Frequency must be between 30 and 1300 MHz.");
            return;
        }

        SetOrClearError(nameof(FrequencyText), null);
    }

    private void ValidateColorCode()
    {
        if (ColorCode is null or < 0 or > 15)
        {
            SetOrClearError(nameof(ColorCode), "Color code must be between 0 and 15.");
            return;
        }

        SetOrClearError(nameof(ColorCode), null);
    }

    private void ValidateTimeslot()
    {
        var allowed = new[] { "1", "2", "XPT" };
        if (Array.IndexOf(allowed, Timeslot) < 0)
        {
            SetOrClearError(nameof(Timeslot), "Select a valid timeslot (1, 2, or XPT).");
            return;
        }

        SetOrClearError(nameof(Timeslot), null);
    }

    private void ValidateTalkGroup()
    {
        SetOrClearError(nameof(TalkGroup), string.IsNullOrWhiteSpace(TalkGroup) ? "Talk group is required." : null);
    }

    private void ValidateContact()
    {
        SetOrClearError(nameof(Contact), string.IsNullOrWhiteSpace(Contact) ? "Contact is required." : null);
    }

    private void ValidatePower()
    {
        if (Power is not "High" and not "Low")
        {
            SetOrClearError(nameof(Power), "Power must be High or Low.");
            return;
        }

        SetOrClearError(nameof(Power), null);
    }

    private void ValidateZone()
    {
        SetOrClearError(nameof(Zone), string.IsNullOrWhiteSpace(Zone) ? "Zone is required." : null);
    }

    private void SetOrClearError(string propertyName, string? error)
    {
        if (string.IsNullOrEmpty(error))
        {
            if (_errors.Remove(propertyName))
            {
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
            }

            return;
        }

        if (!_errors.TryGetValue(propertyName, out var list))
        {
            list = new List<string>();
            _errors[propertyName] = list;
        }
        else
        {
            list.Clear();
        }

        list.Add(error);
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
    }
}
