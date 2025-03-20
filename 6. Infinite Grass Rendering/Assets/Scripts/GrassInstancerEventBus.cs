using Acetix.Events;

namespace Acetix.Grass
{
    public enum InstancerEventType
    {
        INSTANCER_ENTERED,
        INSTANCER_EXITED,
    }

    public class GrassInstancerEventBus : AbstractDataEventBus<InstancerEventType, int> { }
}