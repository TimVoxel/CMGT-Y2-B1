using UnityEngine;

// Plays a track's backing and instrument clips as one unit. Every start, seek and speed change goes through
// Schedule, which restarts both sources on the same future DSP tick, because separate Pause/UnPause/pitch/time
// calls can land in different audio mix blocks and leave the stems apart.
public class SyncedStemPlayback
{
    private const double ScheduleLeadSeconds = 0.05;
    private const float MaxStemDriftSeconds = 0.03f;
    private const float MutedVolumeFactor = 0.0001f;
    public const float MuteFadeSeconds = 0.03f;
    private const float UnmuteFadeSeconds = 0.01f;

    private readonly AudioSource _backingSource;
    private readonly AudioSource _instrumentSource;
    private readonly float _fullVolume;
    private readonly float _leadVolume;

    private AudioClip _backingClip;
    private AudioClip _instrumentClip;
    private float _instrumentVolumeFactor = 1.0f;
    private float _trackInstrumentVolume = 1.0f;
    private bool _isInstrumentMuted;

    public SyncedStemPlayback(AudioSource backingSource, float leadVolume)
    {
        _leadVolume = leadVolume;
        _backingSource = backingSource;
        _instrumentSource = backingSource.gameObject.AddComponent<AudioSource>();
        _fullVolume = backingSource.volume;

        ConfigureLockedSource(_backingSource);
        CopySoundSettings(_backingSource, _instrumentSource);
        ConfigureLockedSource(_instrumentSource);
    }

    public bool IsPlaying => _backingSource.isPlaying || _instrumentSource.isPlaying;
    public float PositionSeconds => _backingSource.time;
    public float LengthSeconds => _backingClip != null ? _backingClip.length : 0.0f;

    public bool IsAudioLoaded => IsLoaded(_backingClip) && IsLoaded(_instrumentClip);

    public void SetClips(Track track)
    {
        Stop();
        _backingClip = track.BackingClip;
        _instrumentClip = track.InstrumentClip;
        _trackInstrumentVolume = track.InstrumentVolume;
        _backingSource.clip = _backingClip;
        _instrumentSource.clip = _instrumentClip;
        RequestLoad(_backingClip);
        RequestLoad(_instrumentClip);
        ReportMismatchedStems(track);
        SetInstrumentMuted(false);
        _instrumentVolumeFactor = 1.0f;
        ApplyInstrumentVolume();
    }

    public void SetInstrumentMuted(bool isMuted)
    {
        _isInstrumentMuted = isMuted;
    }

    public void Schedule(double dspTime, double clipTimeSeconds, float speed)
    {
        Stop();
        ScheduleSource(_backingSource, dspTime, clipTimeSeconds, speed);
        ScheduleSource(_instrumentSource, dspTime, clipTimeSeconds, speed);
    }

    public double ScheduleSoon(double clipTimeSeconds, float speed)
    {
        var dspTime = AudioSettings.dspTime + ScheduleLeadSeconds;
        Schedule(dspTime, clipTimeSeconds, speed);
        return dspTime;
    }

    public void ScheduleInStepWithClock(double clipTimeSeconds, float speed)
    {
        ScheduleSoon(clipTimeSeconds + ScheduleLeadSeconds * speed, speed);
    }

    public void Stop()
    {
        _backingSource.Stop();
        _instrumentSource.Stop();
    }

    public void Tick()
    {
        var targetFactor = _isInstrumentMuted ? MutedVolumeFactor : 1.0f;
        var fadeSeconds = _isInstrumentMuted ? MuteFadeSeconds : UnmuteFadeSeconds;
        _instrumentVolumeFactor = Mathf.MoveTowards(_instrumentVolumeFactor, targetFactor, Time.unscaledDeltaTime / fadeSeconds);
        ApplyInstrumentVolume();

        ResyncIfDrifted();
    }

    private void ResyncIfDrifted()
    {
        if (_backingClip == null || _instrumentClip == null || !_backingSource.isPlaying || !_instrumentSource.isPlaying)
        {
            return;
        }
        var driftSeconds = (_backingSource.timeSamples - _instrumentSource.timeSamples) / (float)_backingClip.frequency;
        if (Mathf.Abs(driftSeconds) <= MaxStemDriftSeconds)
        {
            return;
        }
        Debug.LogWarning($"[Stems] Instrument drifted {driftSeconds * 1000.0f:0.0} ms from backing, resyncing");
        ScheduleInStepWithClock(_backingSource.timeSamples / (double)_backingClip.frequency, _backingSource.pitch);
    }

    private void ApplyInstrumentVolume()
    {
        _instrumentSource.volume = _fullVolume * _leadVolume * _trackInstrumentVolume * _instrumentVolumeFactor;
    }

    private static void ScheduleSource(AudioSource source, double dspTime, double clipTimeSeconds, float speed)
    {
        var clip = source.clip;
        if (clip == null)
        {
            return;
        }
        source.pitch = speed;
        source.timeSamples = Mathf.Clamp((int)System.Math.Round(clipTimeSeconds * clip.frequency), 0, clip.samples - 1);
        source.PlayScheduled(dspTime);
    }

    private static void ConfigureLockedSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = false;
        source.priority = 0;
        source.dopplerLevel = 0.0f;
    }

    private static void CopySoundSettings(AudioSource from, AudioSource to)
    {
        to.outputAudioMixerGroup = from.outputAudioMixerGroup;
        to.volume = from.volume;
        to.panStereo = from.panStereo;
        to.spatialBlend = from.spatialBlend;
        to.reverbZoneMix = from.reverbZoneMix;
        to.spread = from.spread;
        to.rolloffMode = from.rolloffMode;
        to.minDistance = from.minDistance;
        to.maxDistance = from.maxDistance;
        to.bypassEffects = from.bypassEffects;
        to.bypassListenerEffects = from.bypassListenerEffects;
        to.bypassReverbZones = from.bypassReverbZones;
        to.spatialize = from.spatialize;
    }

    private static bool IsLoaded(AudioClip clip) => clip == null || clip.loadState == AudioDataLoadState.Loaded;

    private static void RequestLoad(AudioClip clip)
    {
        if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
        {
            clip.LoadAudioData();
        }
    }

    private void ReportMismatchedStems(Track track)
    {
        if (_instrumentClip == null || _backingClip == null)
        {
            return;
        }
        if (_instrumentClip.samples != _backingClip.samples || _instrumentClip.frequency != _backingClip.frequency)
        {
            Debug.LogError($"[Stems] {track.name}: stems differ ({_backingClip.samples} samples at {_backingClip.frequency} Hz vs " +
                           $"{_instrumentClip.samples} at {_instrumentClip.frequency} Hz). Rebuild them with Tools/build_song.py");
        }
        if (_instrumentClip.loadType == AudioClipLoadType.Streaming || _backingClip.loadType == AudioClipLoadType.Streaming)
        {
            Debug.LogError($"[Stems] {track.name}: streamed clips can't start sample-accurately, use Compressed In Memory");
        }
    }
}
