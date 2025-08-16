using System;
using System.Collections.Generic;

namespace VideoWebPlayer.Services
{
    /// <summary>
    /// Verwaltet die Registrierung und Benachrichtigung von Event-Handlern für verschiedene Event-Typen.
    /// Ermöglicht ein einfaches Publish/Subscribe-Muster innerhalb der Anwendung.
    /// </summary>
    public class EventManager
    {
        // Speichert für jeden Event-Typ eine Liste der zugehörigen Handler (Subscriber).
        private readonly Dictionary<Type, List<Delegate>> _subscribers = new();

        /// <summary>
        /// Registriert einen Handler für einen bestimmten Event-Typ.
        /// </summary>
        /// <typeparam name="TEvent">Der Typ des Events.</typeparam>
        /// <param name="handler">Die Methode, die beim Eintreten des Events aufgerufen werden soll.</param>
        public void Subscribe<TEvent>(Action<TEvent> handler)
        {
            var type = typeof(TEvent);
            if (!_subscribers.ContainsKey(type))
                _subscribers[type] = new List<Delegate>();
            _subscribers[type].Add(handler);
        }

        /// <summary>
        /// Löst ein Event aus und benachrichtigt alle registrierten Handler für diesen Event-Typ.
        /// </summary>
        /// <typeparam name="TEvent">Der Typ des Events.</typeparam>
        /// <param name="evt">Das Event-Objekt, das an die Handler übergeben wird.</param>
        public void Publish<TEvent>(TEvent evt)
        {
            var type = typeof(TEvent);
            if (_subscribers.TryGetValue(type, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    ((Action<TEvent>)handler)?.Invoke(evt);
                }
            }
        }
    }
}