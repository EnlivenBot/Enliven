using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Tyrrrz.Extensions;

namespace Bot.Utilities.Collector {
    public class CollectorsGroup {
        public event EventHandler<CollectorEventArgsBase> RemoveArgsFailed;

        public ObservableCollection<CollectorController> Controllers { get; set; } =
            new ObservableCollection<CollectorController>(new List<CollectorController>());

        public CollectorsGroup() {
            Controllers.CollectionChanged += (sender, args) => {
                try {
                    if (args.NewItems != null)
                        foreach (CollectorController newItem in args.NewItems) {
                            newItem.RemoveArgsFailed += RemoveArgsFailed;
                        }

                    if (args.OldItems != null)
                        foreach (CollectorController oldItem in args.OldItems) {
                            oldItem.RemoveArgsFailed -= RemoveArgsFailed;
                        }
                }
                catch (Exception e) {
                    
                }
            };
        }

        public CollectorsGroup(IEnumerable<CollectorController> controllers) : base() {
            Controllers.AddRange(controllers);
        }

        public CollectorsGroup(params CollectorController[] controllers) : base() {
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