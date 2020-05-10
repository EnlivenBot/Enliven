using System;
using System.Collections.Generic;

namespace Bot.Utilities.Collector {
    public class CollectorsGroup {
        public List<CollectorController> Controllers { get; set; } = new List<CollectorController>();
        public CollectorsGroup() { }

        public CollectorsGroup(IEnumerable<CollectorController> controllers) {
            Controllers.AddRange(controllers);
        }

        public CollectorsGroup(params CollectorController[] controllers) {
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