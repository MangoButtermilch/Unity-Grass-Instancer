namespace Acetix.Events {

    using System;
    using System.Collections.Generic;
    using UnityEngine.Events;

    /// <summary>
    /// Used for event busses that don't need to transmit data.
    /// </summary>
    public abstract class AbstractEventBus<TEventType> where TEventType : Enum {
        private static readonly Dictionary<TEventType, UnityEvent> _events = new Dictionary<TEventType, UnityEvent>();

        public static void Subscribe(TEventType eventType, UnityAction listener) {
            UnityEvent thisEvent;

            if (_events.TryGetValue(eventType, out thisEvent)) {
                thisEvent.AddListener(listener);
            } else {
                thisEvent = new UnityEvent();
                thisEvent.AddListener(listener);
                _events.Add(eventType, thisEvent);
            }
        }

        public static void Unsubscribe(TEventType eventType, UnityAction listener) {
            UnityEvent thisEvent;
            if (_events.TryGetValue(eventType, out thisEvent)) {
                thisEvent.RemoveListener(listener);
            }
        }

        public static void Publish(TEventType eventType) {
            UnityEvent thisEvent;

            if (_events.TryGetValue(eventType, out thisEvent)) {
                thisEvent.Invoke();
            }
        }
    }
}
