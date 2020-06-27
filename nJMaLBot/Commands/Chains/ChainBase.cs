using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Bot.Config.Localization;
using Discord;

namespace Bot.Commands.Chains {
    public abstract class ChainBase {
        public readonly string? Uid;
        private Action<LocalizedEntry> _onEnd = entry => { };

        protected ChainBase(string? uid) {
            Uid = uid;
            if (uid != null) {
                if (_runningChains.TryGetValue(uid, out var previousChain)) {
                    previousChain.OnEnd.Invoke(new LocalizedEntry("ChainsCommon.ReasonStartedNew"));
                }

                _runningChains[uid] = this;
            }
        }

        public IReadOnlyDictionary<string, ChainBase> RunningChains => _runningChains;

        // ReSharper disable once InconsistentNaming
        private ConcurrentDictionary<string, ChainBase> _runningChains { get; set; } = new ConcurrentDictionary<string, ChainBase>();

        private protected EmbedBuilder MainBuilder { get; set; } = new EmbedBuilder();

        private Timer? _timeoutTimer;

        public DateTimeOffset? TimeoutDate { get; private set; }

        public TimeSpan? TimeoutRemain => TimeoutDate.HasValue ? TimeoutDate.Value - DateTimeOffset.Now : (TimeSpan?) null;

        private protected Action<LocalizedEntry> OnEnd {
            get => _onEnd;
            set {
                _onEnd = entry => {
                    if (Uid != null) _runningChains.Remove(Uid, out _);
                    value.Invoke(entry);
                };
            }
        }

        public virtual void SetTimeout(TimeSpan? timeout) {
            if (timeout != null) {
                _timeoutTimer = new Timer(state => { OnEnd.Invoke(new LocalizedEntry("ChainsCommon.ReasonTimeout")); }, null, timeout.Value, TimeSpan.Zero);
                TimeoutDate = DateTimeOffset.Now + timeout;
            }
            else {
                _timeoutTimer = null;
                TimeoutDate = null;
            }
        }
    }
}