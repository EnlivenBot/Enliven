using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bot.Config.Localization.Entries;
using Bot.DiscordRelated;

namespace Bot.Commands.Chains {
    public abstract class ChainBase {
        public static IReadOnlyDictionary<string, ChainBase> RunningChains => _runningChains;

        // ReSharper disable once InconsistentNaming
        private static ConcurrentDictionary<string, ChainBase> _runningChains { get; set; } = new ConcurrentDictionary<string, ChainBase>();

        protected ChainBase(string? uid) {
            Uid = uid;
            if (uid != null) {
                if (_runningChains.TryGetValue(uid, out var previousChain)) {
                    previousChain.OnEnd.Invoke(new EntryLocalized("ChainsCommon.ReasonStartedNew"));
                }

                _runningChains[uid] = this;
            }
        }

        public readonly string? Uid;
        
        private Action<EntryLocalized> _onEnd = entry => { };

        private protected PriorityEmbedBuilderWrapper MainBuilder { get; set; } = new PriorityEmbedBuilderWrapper();

        // ReSharper disable once NotAccessedField.Local
        private Timer? _timeoutTimer;

        public DateTimeOffset? TimeoutDate { get; private set; }

        public TimeSpan? TimeoutRemain => TimeoutDate.HasValue ? TimeoutDate.Value - DateTimeOffset.Now : (TimeSpan?) null;

        private protected Action<EntryLocalized> OnEnd {
            get => _onEnd;
            set {
                _onEnd = entry => {
                    if (IsEnded) return;
                    lock (_lockObject) {

                        IsEnded = true;
                        _onEnd = localizedEntry => {};
                        if (Uid != null) _runningChains.Remove(Uid, out _);
                        foreach (var persistentOnEndAction in PersistentOnEndActions.ToList()) {
                            persistentOnEndAction.Value.Invoke(entry);
                        }
                        PersistentOnEndActions.Clear();
                        value.Invoke(entry);
                    }
                };
            }
        }

        public virtual void SetTimeout(TimeSpan? timeout) {
            if (timeout != null) {
                _timeoutTimer = new Timer(state => { OnEnd.Invoke(new EntryLocalized("ChainsCommon.ReasonTimeout")); }, null, timeout.Value, TimeSpan.Zero);
                TimeoutDate = DateTimeOffset.Now + timeout;
            }
            else {
                _timeoutTimer = null;
                TimeoutDate = null;
            }
        }
        
        public bool IsEnded { get; private set; }

        private protected readonly Dictionary<string, Action<EntryLocalized>> PersistentOnEndActions = new Dictionary<string, Action<EntryLocalized>>();
        private readonly object _lockObject = new object();
    }
}