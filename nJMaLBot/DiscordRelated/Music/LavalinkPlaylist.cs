using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lavalink4NET.Player;

namespace Bot.DiscordRelated.Music {
    public sealed class LavalinkPlaylist : IList<LavalinkTrack> {
        private readonly List<LavalinkTrack> _list;
        private readonly object _syncRoot;
        public event EventHandler? Update;

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
                    OnUpdate();
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
            lock (_syncRoot) {
                _list.Add(track);
                OnUpdate();
            }
        }

        public void AddRange(IEnumerable<LavalinkTrack> tracks) {
            if (tracks == null) throw new ArgumentNullException(nameof(tracks));
            lock (_syncRoot) {
                _list.AddRange(tracks);
                OnUpdate();
            }
        }

        public int Clear() {
            lock (_syncRoot) {
                int count = _list.Count;
                _list.Clear();
                OnUpdate();
                return count;
            }
        }

        void ICollection<LavalinkTrack>.Clear() {
            lock (_syncRoot) {
                _list.Clear();
                OnUpdate();
            }
        }

        public bool Contains(LavalinkTrack track) {
            if (track == null) throw new ArgumentNullException(nameof(track));
            lock (_syncRoot) return _list.Contains(track);
        }

        public void CopyTo(LavalinkTrack[] array, int index) {
            lock (_syncRoot) _list.CopyTo(array, index);
        }

        public bool TryGetValue(int index, out LavalinkTrack? track) {
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

        public void Distinct() {
            lock (_syncRoot) {
                if (_list.Count <= 1) return;
                LavalinkTrack[] array = _list.GroupBy(track => track.Identifier).Select(s => s.First()).ToArray();
                _list.Clear();
                _list.AddRange(array);
                OnUpdate();
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
            lock (_syncRoot) {
                _list.Insert(index, track);
                OnUpdate();
            }
        }
        
        public void InsertRange(int index, IEnumerable<LavalinkTrack> tracks) {
            lock (_syncRoot) {
                var lavalinkTracks = tracks.ToList();
                for (var i = 0; i < lavalinkTracks.Count; i++) {
                    var track = lavalinkTracks[i];
                    _list.Insert(index + i, track);
                }
                OnUpdate();
            }
        }

        public bool Remove(LavalinkTrack track) {
            lock (_syncRoot) {
                var result = _list.Remove(track);
                OnUpdate();
                return result;
            }
        }

        public int RemoveAll(Predicate<LavalinkTrack> predicate) {
            lock (_syncRoot) {
                var result =  _list.RemoveAll(predicate);
                OnUpdate();
                return result;
            }
        }

        public void RemoveAt(int index) {
            lock (_syncRoot) {
                _list.RemoveAt(index);
                OnUpdate();
            }
        }

        public void RemoveRange(int index, int count) {
            lock (_syncRoot) {
                _list.RemoveRange(index, count);
                OnUpdate();
            }
        }

        public void Shuffle() {
            lock (_syncRoot) {
                if (_list.Count <= 2) return;
                LavalinkTrack[] array = _list.OrderBy(s => Guid.NewGuid()).ToArray();
                _list.Clear();
                _list.AddRange(array);
                OnUpdate();
            }
        }

        public bool TryDequeue(out LavalinkTrack? track) {
            lock (_syncRoot) {
                if (_list.Count <= 0) {
                    track = null;
                    return false;
                }

                track = _list[0];
                _list.RemoveAt(0);
                OnUpdate();
                return true;
            }
        }

        public void Move(int oldIndex, int newIndex) {
            lock (_syncRoot) {
                try {
                    var track = _list[oldIndex];
                    _list.RemoveAt(oldIndex);
                    _list.Insert(newIndex, track);
                }
                finally {
                    OnUpdate();
                }
            }
        }

        private void OnUpdate() {
            Update?.Invoke(this, EventArgs.Empty);
        }
    }
}