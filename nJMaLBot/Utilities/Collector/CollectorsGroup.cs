using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Tyrrrz.Extensions;
#pragma warning disable 8606

namespace Bot.Utilities.Collector {
    public class CollectorsGroup {
        public event EventHandler<CollectorEventArgsBase>? RemoveArgsFailed;

        public ObservableCollection<CollectorController> Controllers { get; set; } =
            new ObservableCollection<CollectorController>(new List<CollectorController>());

        public CollectorsGroup() {
            Controllers.CollectionChanged += (sender, args) => {
                try {
                    if (args.NewItems != null)
                        foreach (CollectorController? newItem in args.NewItems) {
                            if (newItem != null) newItem.RemoveArgsFailed += RemoveArgsFailed;
                        }

                    if (args.OldItems != null)
                        foreach (CollectorController? oldItem in args.OldItems) {
                            if (oldItem != null) oldItem.RemoveArgsFailed -= RemoveArgsFailed;
                        }
                }
                catch (Exception) {
                    // ignored
                }
            };
        }

        public CollectorsGroup(IEnumerable<CollectorController> controllers) {
            Controllers.AddRange(controllers);
        }

        public CollectorsGroup(params CollectorController[] controllers) {
            Controllers.AddRange(controllers);
        }

        public void Add(params CollectorController[] controllers) {
            Controllers.AddRange(controllers);
        }

        public void SetTimeoutToAll(TimeSpan timeout) {
            foreach (var controller in Controllers) {
                controller.SetTimeout(timeout);
            }
        }

        public void DisposeAll() {
            while (Controllers.Count != 0) {
                Controllers[0].Dispose();
                Controllers.RemoveAt(0);
            }
        }
    }
}