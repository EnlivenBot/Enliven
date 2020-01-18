using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lavalink4NET.Player;

namespace Bot.Music.Players {
    public sealed class LavalinkPlaylist : IList<LavalinkTrack> {
        private readonly List<LavalinkTrack> _list;
        private readonly object _syncRoot;

        public LavalinkPlaylist() {
            _list = new List<LavalinkTrack>();
            _syncRoot = new object();
        }

        public int Count {
            get {
                lock (_syncRoot) return _list.Count;
            }
        }

        public bool IsEmpty {
            get { return Count == 0; }
        }

        public bool IsReadOnly {
            get { return true; }
        }

        public IReadOnlyList<LavalinkTrack> Tracks {
            get {
                lock (_syncRoot) return _list.ToArray();
            }
            set {
                lock (_syncRoot) {
                    _list.Clear();
                    _list.AddRange(value);
                }
            }
        }

        public LavalinkTrack this[int index] {
            get {
                lock (_syncRoot) return _list[index];
            }
            set {
                if (value == null) throw new ArgumentNullException(nameof(value));
                lock (_syncRoot) _list[index] = value;
            }
        }

        public void Add(LavalinkTrack track) {
            if (track == null) throw new ArgumentNullException(nameof(track));
            lock (_syncRoot) _list.Add(track);
        }

        public void AddRange(IEnumerable<LavalinkTrack> tracks) {
            if (tracks == null) throw new ArgumentNullException(nameof(tracks));
            lock (_syncRoot) _list.AddRange(tracks);
        }

        public int Clear() {
            lock (_syncRoot) {
                int count = _list.Count;
                _list.Clear();
                return count;
            }
        }

        void ICollection<LavalinkTrack>.Clear() {
            lock (_syncRoot) _list.Clear();
        }

        public bool Contains(LavalinkTrack track) {
            if (track == null) throw new ArgumentNullException(nameof(track));
            lock (_syncRoot) return _list.Contains(track);
        }

        public void CopyTo(LavalinkTrack[] array, int index) {
            lock (_syncRoot) _list.CopyTo(array, index);
        }

        public bool TryGetValue(int index, out LavalinkTrack track) {
            lock (_syncRoot) {
                track = null;
                try {
                    track = _list[index];
                    return track != null;
                }
                catch (Exception) {
                    return false;
                }
            }
        }

        public LavalinkTrack Dequeue() {
            lock (_syncRoot) {
                if (_list.Count <= 0) throw new InvalidOperationException("No tracks in to dequeue.");
                LavalinkTrack lavalinkTrack = _list[0];
                _list.RemoveAt(0);
                return lavalinkTrack;
            }
        }

        public void Distinct() {
            lock (_syncRoot) {
                if (_list.Count <= 1) return;
                LavalinkTrack[] array = _list.GroupBy(track => track.Identifier).Select(s => s.First()).ToArray();
                _list.Clear();
                _list.AddRange(array);
            }
        }

        public IEnumerator<LavalinkTrack> GetEnumerator() {
            lock (_syncRoot) return _list.ToList().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            lock (_syncRoot) return _list.ToArray().GetEnumerator();
        }

        public int IndexOf(LavalinkTrack track) {
            if (track == null) throw new ArgumentNullException(nameof(track));
            lock (_syncRoot) return _list.IndexOf(track);
        }

        public void Insert(int index, LavalinkTrack track) {
            lock (_syncRoot) _list.Insert(index, track);
        }

        public bool Remove(LavalinkTrack track) {
            lock (_syncRoot) return _list.Remove(track);
        }

        public int RemoveAll(Predicate<LavalinkTrack> predicate) {
            lock (_syncRoot) return _list.RemoveAll(predicate);
        }

        public void RemoveAt(int index) {
            lock (_syncRoot) _list.RemoveAt(index);
        }

        public void RemoveRange(int index, int count) {
            lock (_syncRoot) _list.RemoveRange(index, count);
        }

        public void Shuffle() {
            lock (_syncRoot) {
                if (_list.Count <= 2) return;
                LavalinkTrack[] array = _list.OrderBy(s => Guid.NewGuid()).ToArray();
                _list.Clear();
                _list.AddRange(array);
            }
        }

        public bool TryDequeue(out LavalinkTrack track) {
            lock (_syncRoot) {
                if (_list.Count <= 0) {
                    track = null;
                    return false;
                }

                track = _list[0];
                _list.RemoveAt(0);
                return true;
            }
        }
    }
}