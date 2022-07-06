using System;
using System.Collections.Generic;
using Lavalink4NET.Player;

namespace Common.Music.Tracks {
    public static class TrackAttributesExtension {
        /// <summary>
        /// Add or update attribute
        /// </summary>
        /// <param name="track">Target track</param>
        /// <param name="requesterTrackAttribute">Target</param>
        /// <param name="replace">If true existent attribute will be replaced</param>
        /// <typeparam name="T">Attribute type</typeparam>
        public static void AddAttribute<T>(this LavalinkTrack track, T requesterTrackAttribute, bool replace = true) where T : ILavalinkTrackAttribute{
            if (replace || !track.TryGetAttribute<T>(out _)) {
                track.GetAttributes()[typeof(T)] = requesterTrackAttribute;
            }
        }

        /// <summary>
        /// Get track attribute
        /// </summary>
        /// <param name="track">Target track</param>
        /// <param name="attribute">Out attribute</param>
        /// <typeparam name="T">Attribute type</typeparam>
        /// <returns>True if attribute exists</returns>
        public static bool TryGetAttribute<T>(this LavalinkTrack track, out T attribute) where T : ILavalinkTrackAttribute {
            attribute = default!;
            if (!track.GetAttributes().TryGetValue(typeof(T), out var value)) return false;
            attribute = (T) value;
            return true;
        }

        /// <summary>
        /// Get attribute if exists or create new one, add it and return
        /// </summary>
        /// <param name="track">Target track</param>
        /// <param name="factory">New attribute factory</param>
        /// <typeparam name="T">Attribute type</typeparam>
        /// <returns>Target attribute</returns>
        public static T GetOrAddAttribute<T>(this LavalinkTrack track, Func<T> factory) where T : ILavalinkTrackAttribute {
            if (track.TryGetAttribute(out T existentAttribute)) {
                return existentAttribute;
            }
            
            return (T) (track.GetAttributes()[typeof(T)] = factory());
        }

        private static Dictionary<Type, ILavalinkTrackAttribute> GetAttributes(this LavalinkTrack track) {
            track.Context ??= new Dictionary<Type, ILavalinkTrackAttribute>();
            return (Dictionary<Type, ILavalinkTrackAttribute>)track.Context;
        }
    }
}