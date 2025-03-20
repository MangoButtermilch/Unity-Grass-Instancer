using System;
using System.Collections.Generic;
using UnityEngine.Events;

namespace Acetix.Events {

    /// <summary>
    /// Used for event busses that need to transmit data.
    /// </summary>
    public class AbstractDataEventBus<TEventType, TEventArgs> where TEventType : Enum {
        private static readonly Dictionary<TEventType, UnityEvent<TEventArgs>> _events = new Dictionary<TEventType, UnityEvent<TEventArgs>>();

        public static void Subscribe(TEventType eventType, UnityAction<TEventArgs> listener) {
            UnityEvent<TEventArgs> thisEvent;

            if (_events.TryGetValue(eventType, out thisEvent)) {
                thisEvent.AddListener(listener);
            } else {
                thisEvent = new UnityEvent<TEventArgs>();
                thisEvent.AddListener(listener);
                _events.Add(eventType, thisEvent);
            }
        }

        public static void Unsubscribe(TEventType eventType, UnityAction<TEventArgs> listener) {
            UnityEvent<TEventArgs> thisEvent;
            if (_events.TryGetValue(eventType, out thisEvent)) {
                thisEvent.RemoveListener(listener);
            }
        }

        public static void Publish(TEventType eventType, TEventArgs eventArgs) {
            UnityEvent<TEventArgs> thisEvent;

            if (_events.TryGetValue(eventType, out thisEvent)) {
                thisEvent.Invoke(eventArgs);
            }
        }
    }
}
