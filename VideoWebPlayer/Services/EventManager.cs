using System;
using System.Collections.Generic;
using System.Linq;

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
        private readonly object _lock = new();

        /// <summary>
        /// Registriert einen Handler für einen bestimmten Event-Typ.
        /// </summary>
        /// <typeparam name="TEvent">Der Typ des Events.</typeparam>
        /// <param name="handler">Die Methode, die beim Eintreten des Events aufgerufen werden soll.</param>
        public void Subscribe<TEvent>(Action<TEvent> handler)
        {
            var type = typeof(TEvent);
            lock (_lock)
            {
                if (!_subscribers.ContainsKey(type))
                    _subscribers[type] = new List<Delegate>();
                _subscribers[type].Add(handler);
            }
        }

        /// <summary>
        /// Registriert einen Handler für einen bestimmten Event-Typ und liefert ein IDisposable zum Abmelden.
        /// </summary>
        public IDisposable SubscribeDisposable<TEvent>(Action<TEvent> handler)
        {
            Subscribe(handler);
            return new Subscription(() => Unsubscribe(handler));
        }

        /// <summary>
        /// Entfernt einen zuvor registrierten Handler für einen bestimmten Event-Typ.
        /// </summary>
        public void Unsubscribe<TEvent>(Action<TEvent> handler)
        {
            var type = typeof(TEvent);
            lock (_lock)
            {
                if (!_subscribers.TryGetValue(type, out var handlers))
                    return;

                handlers.Remove(handler);
                if (handlers.Count == 0)
                    _subscribers.Remove(type);
            }
        }

        /// <summary>
        /// Löst ein Event aus und benachrichtigt alle registrierten Handler für diesen Event-Typ.
        /// </summary>
        /// <typeparam name="TEvent">Der Typ des Events.</typeparam>
        /// <param name="evt">Das Event-Objekt, das an die Handler übergeben wird.</param>
        public void Publish<TEvent>(TEvent evt)
        {
            var type = typeof(TEvent);
            List<Delegate>? handlersCopy = null;

            lock (_lock)
            {
                if (_subscribers.TryGetValue(type, out var handlers))
                    handlersCopy = handlers.ToList();
            }

            if (handlersCopy == null)
                return;

            foreach (var handler in handlersCopy)
                ((Action<TEvent>)handler)?.Invoke(evt);
        }

        private sealed class Subscription : IDisposable
        {
            private readonly Action _dispose;
            private bool _isDisposed;

            public Subscription(Action dispose) => _dispose = dispose;

            public void Dispose()
            {
                if (_isDisposed)
                    return;

                _isDisposed = true;
                _dispose();
            }
        }
    }
}