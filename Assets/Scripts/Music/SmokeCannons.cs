using UnityEngine;

public class SmokeCannons : MonoBehaviour
{
    private const double MaxSongTimeStepSeconds = 0.5;

    [SerializeField] private TrackPlayer _trackPlayer;
    [SerializeField] private ParticleSystem[] _cannons;

    private Track _lastTrack;
    private double _lastSongTime;

    private void Update()
    {
        if (!_trackPlayer.IsPlaying)
        {
            _lastTrack = null;
            return;
        }

        var track = _trackPlayer.Track;
        var songTime = _trackPlayer.SongTime;
        var isContinuous = track == _lastTrack && songTime >= _lastSongTime && songTime - _lastSongTime <= MaxSongTimeStepSeconds;
        if (isContinuous && HasSmokeBarBetween(track, _lastSongTime, songTime))
        {
            Fire();
        }
        _lastTrack = track;
        _lastSongTime = songTime;
    }

    private static bool HasSmokeBarBetween(Track track, double fromExclusive, double toInclusive)
    {
        foreach (var bar in track.SmokeBars)
        {
            var barTime = track.GetBarTime(bar);
            if (barTime > fromExclusive && barTime <= toInclusive)
            {
                return true;
            }
        }
        return false;
    }

    private void Fire()
    {
        foreach (var cannon in _cannons)
        {
            cannon.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            cannon.Play(true);
        }
    }
}
