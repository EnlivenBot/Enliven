using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bot.Config.Localization.Entries;
using Bot.Config.Localization.Providers;
using Bot.DiscordRelated;
using Bot.Utilities;

namespace Bot.Commands.Chains {
    public abstract class ChainBase {
        private readonly object _lockObject = new object();


        private protected readonly Dictionary<string, Action<EntryLocalized>> PersistentOnEndActions = new Dictionary<string, Action<EntryLocalized>>();

        public readonly string? Uid;

        private Action<EntryLocalized> _onEnd = entry => { };

        // ReSharper disable once NotAccessedField.Local
        private Timer? _timeoutTimer;

        protected ChainBase(string? uid, ILocalizationProvider loc) {
            Loc = loc.ToContainer();
            Uid = uid;
            Loc.LanguageChanged += (sender, args) => Update();
            if (uid != null) {
                if (_runningChains.TryGetValue(uid, out var previousChain)) {
                    previousChain.OnEnd.Invoke(new EntryLocalized("ChainsCommon.ReasonStartedNew"));
                }

                _runningChains[uid] = this;
            }
        }

        public LocalizationContainer Loc { get; set; }
        public static IReadOnlyDictionary<string, ChainBase> RunningChains => _runningChains;

        // ReSharper disable once InconsistentNaming
        private static ConcurrentDictionary<string, ChainBase> _runningChains { get; set; } = new ConcurrentDictionary<string, ChainBase>();

        private protected PriorityEmbedBuilderWrapper MainBuilder { get; set; } = new PriorityEmbedBuilderWrapper();

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
                            try {
                                persistentOnEndAction.Value.Invoke(entry);
                            }
                            catch (Exception) {
                                // ignored
                            }
                        }
                        PersistentOnEndActions.Clear();
                        try {
                            value.Invoke(entry);
                        }
                        catch (Exception) {
                            // ignored
                        }
                    }
                };
            }
        }

        public bool IsEnded { get; private set; }

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

        public virtual void Update() { }
    }
}