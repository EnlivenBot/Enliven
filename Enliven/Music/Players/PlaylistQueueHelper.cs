using System;
using System.Collections;
using System.Collections.Generic;
using Common.Music;
using Common.Music.Tracks;

namespace Bot.Music.Players;

public sealed class PlaylistQueueHelper : IEnumerator<PlaylistQueueHelper.GroupedTracks> {
    private readonly int _endIndex;
    private readonly LavalinkPlaylist _playlist;
    private int _currentIndex;

    public PlaylistQueueHelper(LavalinkPlaylist playlist, int beginIndex, int count) {
        _playlist = playlist;
        _currentIndex = Math.Max(beginIndex, 0) - 1;
        _endIndex = Math.Clamp(beginIndex + count, 0, playlist.Count);
    }

    public bool MoveNext() {
        var startIndex = _currentIndex + 1;
        while (++_currentIndex < _endIndex && _playlist[_currentIndex].Requester == _playlist[startIndex].Requester) { }

        if (_currentIndex == startIndex)
            return false;

        _currentIndex--;

        Current = new GroupedTracks(_playlist, _playlist[startIndex].Requester, startIndex,
            _currentIndex - startIndex + 1);
        return true;
    }

    public void Reset() {
        throw new NotSupportedException();
    }

    public GroupedTracks Current { get; private set; } = null!;

    object IEnumerator.Current => Current;

    public void Dispose() {
    }

    public class GroupedTracks(LavalinkPlaylist playlist, TrackRequester requester, int startIndex, int count)
        : IEnumerator<IEnlivenQueueItem> {
        public TrackRequester Requester { get; } = requester;
        private int _currentIndex = startIndex - 1;

        public bool MoveNext() {
            if (++_currentIndex >= startIndex + count) {
                return false;
            }

            Current = playlist[_currentIndex];
            return true;
        }

        public void Reset() {
            throw new NotSupportedException();
        }

        public int Count => count;
        public int CurrentIndex => _currentIndex;
        public int CurrentNumber => _currentIndex + 1;

        public bool CurrentIsLast => _currentIndex == startIndex + count - 1;

        public bool CurrentIsLastButGroupContinues =>
            CurrentIsLast && (!playlist.TryGetValue(_currentIndex + 1, out var nextTrack)
                              || nextTrack.Requester != Requester);

        public IEnlivenQueueItem Current { get; private set; } = null!;

        object? IEnumerator.Current => Current;

        public void Dispose() { }
    }
}