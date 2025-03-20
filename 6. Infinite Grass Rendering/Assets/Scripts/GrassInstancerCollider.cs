using UnityEngine;

namespace Acetix.Grass
{
    public class GrassInstancerCollider : MonoBehaviour
    {
        public int Id;

        public void OnTriggerEnter(Collider other)
        {
            GrassInstancerEventBus.Publish(InstancerEventType.INSTANCER_ENTERED, Id);
        }

        public void OnTriggerExit(Collider other)
        {
            GrassInstancerEventBus.Publish(InstancerEventType.INSTANCER_EXITED, Id);
        }
    }
}