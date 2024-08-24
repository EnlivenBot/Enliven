using System;
using System.Collections;
using System.Collections.Generic;
using Common.Music.Tracks;

namespace Common.Music.Players;

public sealed class PlaylistQueueHelper : IEnumerator<IEnlivenQueueItem>
{
    private readonly int _endIndex;
    private readonly LavalinkPlaylist _playlist;
    private IEnlivenQueueItem _current = null!;
    private int _currentIndex;
    private IEnlivenQueueItem? _next;
    private IEnlivenQueueItem? _previous;

    public PlaylistQueueHelper(LavalinkPlaylist playlist, int beginIndex, int count)
    {
        _playlist = playlist;
        _endIndex = beginIndex + count;
        _currentIndex = Math.Max(beginIndex, 0) - 1;
        playlist.TryGetValue(_currentIndex + 1, out _next);
    }

    public int CurrentTrackNumber => _currentIndex + 1;
    public int CurrentTrackIndex => _currentIndex;
    public bool IsFirstInGroup => _previous == null || _previous.Requester != _current.Requester;
    public bool IsLastInGroup => _next == null || _next.Requester != _current.Requester;

    public bool MoveNext()
    {
        if (_currentIndex >= _endIndex)
            return false;

        if (_next == null)
            return false;

        _currentIndex++;
        _previous = _current;
        _current = _next;
        _playlist.TryGetValue(_currentIndex + 1, out _next);
        return true;
    }

    public void Reset()
    {
        throw new NotSupportedException();
    }

    public IEnlivenQueueItem Current => _current;

    object IEnumerator.Current => Current;

    public void Dispose()
    {
    }
}