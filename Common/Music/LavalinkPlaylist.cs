using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using Common.Music.Tracks;
using Tyrrrz.Extensions;

namespace Common.Music;

public sealed class LavalinkPlaylist : IList<IEnlivenQueueItem>
{
    private readonly ISubject<LavalinkPlaylist> _changed = new Subject<LavalinkPlaylist>();
    private readonly List<IEnlivenQueueItem> _list;
    private readonly object _syncRoot;
    private TimeSpan? _totalPlaylistLength;

    public LavalinkPlaylist()
    {
        _list = new List<IEnlivenQueueItem>();
        _syncRoot = new object();
    }

    public bool IsEmpty => Count == 0;

    public IReadOnlyList<IEnlivenQueueItem> Tracks
    {
        get
        {
            lock (_syncRoot) return _list.ToArray();
        }
        set
        {
            lock (_syncRoot)
            {
                _list.Clear();
                _list.AddRange(value);
                OnUpdate();
            }
        }
    }

    public TimeSpan TotalPlaylistLength => _totalPlaylistLength ??=
        Tracks.Sum(track => track.Track?.IsSeekable ?? false ? track.Track.Duration : TimeSpan.Zero);

    public IObservable<LavalinkPlaylist> Changed => _changed.AsObservable();

    public int Count
    {
        get
        {
            lock (_syncRoot) return _list.Count;
        }
    }

    public bool IsReadOnly => true;

    public IEnlivenQueueItem this[int index]
    {
        get
        {
            lock (_syncRoot) return _list[index];
        }
        set
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            lock (_syncRoot) _list[index] = value;
        }
    }

    public void Add(IEnlivenQueueItem track)
    {
        if (track == null) throw new ArgumentNullException(nameof(track));
        lock (_syncRoot)
        {
            _list.Add(track);
            OnUpdate();
        }
    }

    void ICollection<IEnlivenQueueItem>.Clear()
    {
        lock (_syncRoot)
        {
            _list.Clear();
            OnUpdate();
        }
    }

    public bool Contains(IEnlivenQueueItem track)
    {
        if (track == null) throw new ArgumentNullException(nameof(track));
        lock (_syncRoot) return _list.Contains(track);
    }

    public void CopyTo(IEnlivenQueueItem[] array, int index)
    {
        lock (_syncRoot) _list.CopyTo(array, index);
    }

    public IEnumerator<IEnlivenQueueItem> GetEnumerator()
    {
        lock (_syncRoot) return _list.ToList().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        lock (_syncRoot) return _list.ToArray().GetEnumerator();
    }

    public int IndexOf(IEnlivenQueueItem track)
    {
        if (track == null) throw new ArgumentNullException(nameof(track));
        lock (_syncRoot) return _list.IndexOf(track);
    }

    public void Insert(int index, IEnlivenQueueItem track)
    {
        lock (_syncRoot)
        {
            _list.Insert(index, track);
            OnUpdate();
        }
    }

    public bool Remove(IEnlivenQueueItem track)
    {
        lock (_syncRoot)
        {
            var result = _list.Remove(track);
            OnUpdate();
            return result;
        }
    }

    public void RemoveAt(int index)
    {
        lock (_syncRoot)
        {
            _list.RemoveAt(index);
            OnUpdate();
        }
    }

    public int IndexOfWithFallback(IEnlivenQueueItem track)
    {
        if (track == null) throw new ArgumentNullException(nameof(track));
        lock (_syncRoot)
        {
            var index = _list.IndexOf(track);
            if (index != -1)
                return index;
            return _list.IndexOf(item => CustomItemsEquals(track, item));
        }
    }

    private bool CustomItemsEquals(IEnlivenQueueItem first, IEnlivenQueueItem other)
    {
        if (ReferenceEquals(first, other))
        {
            return true; // same instance
        }

        static bool AdditionalInformationEquals(
            IImmutableDictionary<string, JsonElement>? a,
            IImmutableDictionary<string, JsonElement>? b)
        {
            if (a is null || b is null)
            {
                return a is null && b is null;
            }

            if (a.Count != b.Count)
            {
                return false;
            }

            foreach (var (key, value) in a)
            {
                if (!b.TryGetValue(key, out var otherValue) || !value.Equals(otherValue))
                {
                    return false;
                }
            }

            return true;
        }

        return EqualityComparer<TrackRequester>.Default.Equals(first.Requester, other.Requester)
               && EqualityComparer<TrackPlaylist>.Default.Equals(first.Playlist, other.Playlist)
               && EqualityComparer<string>.Default.Equals(first.Track.Title, other.Track.Title)
               && EqualityComparer<string>.Default.Equals(first.Track.Identifier, other.Track.Identifier)
               && EqualityComparer<string>.Default.Equals(first.Track.Author, other.Track.Author)
               && EqualityComparer<TimeSpan>.Default.Equals(first.Track.Duration, other.Track.Duration)
               && EqualityComparer<bool>.Default.Equals(first.Track.IsLiveStream, other.Track.IsLiveStream)
               && EqualityComparer<bool>.Default.Equals(first.Track.IsSeekable, other.Track.IsSeekable)
               && EqualityComparer<Uri>.Default.Equals(first.Track.Uri, other.Track.Uri)
               && EqualityComparer<Uri>.Default.Equals(first.Track.ArtworkUri, other.Track.ArtworkUri)
               && EqualityComparer<string>.Default.Equals(first.Track.Isrc, other.Track.Isrc)
               && EqualityComparer<string>.Default.Equals(first.Track.SourceName, other.Track.SourceName)
               // && EqualityComparer<TimeSpan?>.Default.Equals(first.Track.StartPosition, other.Track.StartPosition)
               && EqualityComparer<string>.Default.Equals(first.Track.ProbeInfo, other.Track.ProbeInfo)
               && AdditionalInformationEquals(first.Track.AdditionalInformation, other.Track.AdditionalInformation);
    }

    public void AddRange(IEnumerable<IEnlivenQueueItem> tracks)
    {
        if (tracks == null) throw new ArgumentNullException(nameof(tracks));
        lock (_syncRoot)
        {
            _list.AddRange(tracks);
            OnUpdate();
        }
    }

    public int Clear()
    {
        lock (_syncRoot)
        {
            int count = _list.Count;
            _list.Clear();
            OnUpdate();
            return count;
        }
    }

    public bool TryGetValue(int index, [NotNullWhen(true)] out IEnlivenQueueItem? track)
    {
        lock (_syncRoot)
        {
            track = null;
            try
            {
                track = _list[index];
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public void Distinct()
    {
        lock (_syncRoot)
        {
            if (_list.Count <= 1) return;
            IEnlivenQueueItem[] array = _list.GroupBy(track => track.Identifier).Select(s => s.First()).ToArray();
            _list.Clear();
            _list.AddRange(array);
            OnUpdate();
        }
    }

    public void InsertRange(int index, IEnumerable<IEnlivenQueueItem> tracks)
    {
        lock (_syncRoot)
        {
            var enlivenQueueItems = tracks.ToList();
            for (var i = 0; i < enlivenQueueItems.Count; i++)
            {
                var track = enlivenQueueItems[i];
                _list.Insert(index + i, track);
            }

            OnUpdate();
        }
    }

    public int RemoveAll(Predicate<IEnlivenQueueItem> predicate)
    {
        lock (_syncRoot)
        {
            var result = _list.RemoveAll(predicate);
            OnUpdate();
            return result;
        }
    }

    public void RemoveRange(int index, int count)
    {
        lock (_syncRoot)
        {
            _list.RemoveRange(index, count);
            OnUpdate();
        }
    }

    public void Shuffle()
    {
        lock (_syncRoot)
        {
            if (_list.Count <= 2) return;
            IEnlivenQueueItem[] array = _list.OrderBy(s => Guid.NewGuid()).ToArray();
            _list.Clear();
            _list.AddRange(array);
            OnUpdate();
        }
    }

    public bool TryDequeue(out IEnlivenQueueItem? track)
    {
        lock (_syncRoot)
        {
            if (_list.Count <= 0)
            {
                track = null;
                return false;
            }

            track = _list[0];
            _list.RemoveAt(0);
            OnUpdate();
            return true;
        }
    }

    public void Move(int oldIndex, int newIndex)
    {
        lock (_syncRoot)
        {
            try
            {
                var track = _list[oldIndex];
                _list.RemoveAt(oldIndex);
                _list.Insert(newIndex, track);
            }
            finally
            {
                OnUpdate();
            }
        }
    }

    private void OnUpdate()
    {
        _totalPlaylistLength = null;
        _changed.OnNext(this);
    }
}