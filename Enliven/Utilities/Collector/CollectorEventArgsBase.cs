using System;
using System.Threading.Tasks;

namespace Bot.Utilities.Collector;

public abstract class CollectorEventArgsBase : EventArgs {
    protected CollectorEventArgsBase(CollectorController controller) {
        Controller = controller;
    }

    public CollectorController Controller { get; set; }

    public void StopCollect() {
        Controller.Dispose();
    }

    public abstract Task RemoveReason();
}