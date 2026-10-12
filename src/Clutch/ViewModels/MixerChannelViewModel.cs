using Clutch.Core.Audio;
using Clutch.UI.Controls;

namespace Clutch.ViewModels;

/// <summary>Mikserde tek bir kanal: ad, seviye göstergesi, ses kaydırıcısı, sessize alma.</summary>
public sealed class MixerChannelViewModel : ViewModelBase
{
    private const double PeakHoldSeconds = 1.2;
    private const double FallDbPerTick = 1.6;

    private readonly AudioChannelState _state;
    private readonly Action<double> _onVolumeChanged;
    private double _volume;
    private double _levelDb = LevelMeter.MinDb;
    private double _peakDb = LevelMeter.MinDb;
    private DateTime _peakAt;
    private bool _isRecorded;
    private bool _isAvailable = true;

    public MixerChannelViewModel(string name, string icon, AudioChannelState state, double volume, Action<double> onVolumeChanged)
    {
        Name = name;
        Icon = icon;
        _state = state;
        _volume = volume;
        _onVolumeChanged = onVolumeChanged;
    }

    public string Name { get; }
    public string Icon { get; }

    /// <summary>Ayarlarda bu kaynak kayda alınıyor mu (Kaynaklar panelindeki göz).</summary>
    public bool IsRecorded
    {
        get => _isRecorded;
        set => SetProperty(ref _isRecorded, value);
    }

    /// <summary>Cihaz takılı mı (mikrofon yoksa false).</summary>
    public bool IsAvailable
    {
        get => _isAvailable;
        set => SetProperty(ref _isAvailable, value);
    }

    public double Volume
    {
        get => _volume;
        set
        {
            if (SetProperty(ref _volume, Math.Clamp(value, 0, 1)))
            {
                _onVolumeChanged(_volume);
                OnPropertyChanged(nameof(VolumeText));
            }
        }
    }

    public string VolumeText => $"%{Math.Round(_volume * 100)}";

    public bool IsMuted
    {
        get => _state.Muted;
        set
        {
            _state.Muted = value;
            OnPropertyChanged();
        }
    }

    public double LevelDb
    {
        get => _levelDb;
        private set => SetProperty(ref _levelDb, value);
    }

    public double PeakDb
    {
        get => _peakDb;
        private set => SetProperty(ref _peakDb, value);
    }

    /// <summary>~30 Hz çağrılır. Gösterge yükselirken anında, düşerken yavaşça hareket eder (OBS balistiği).</summary>
    public void Tick(float peakLinear)
    {
        var db = peakLinear <= 0.000_001f ? LevelMeter.MinDb : Math.Max(LevelMeter.MinDb, 20 * Math.Log10(peakLinear));

        LevelDb = db >= LevelDb ? db : Math.Max(db, LevelDb - FallDbPerTick);

        var now = DateTime.UtcNow;
        if (db >= PeakDb)
        {
            PeakDb = db;
            _peakAt = now;
        }
        else if ((now - _peakAt).TotalSeconds > PeakHoldSeconds)
        {
            PeakDb = Math.Max(LevelMeter.MinDb, PeakDb - FallDbPerTick);
        }
    }
}
