using System;
using System.Linq;

namespace VideoPlayer.Service.Events
{
    public class NotificationEventArgs: EventArgs
    {

        public NotificationEventArgs(string name, object data)
        {
            Data = data;
            Name = name;
        }

        public string Name { get; private set; }

        public object Data { get; private set; }

    }

    public interface IEventSubscriber
    {

        void ProcessNotification(object sender, NotificationEventArgs e);

    }

    public interface IEventPublisher
    {

        void Notify(object sender, NotificationEventArgs e);

        event EventHandler<NotificationEventArgs> OnEvent;

    }

    public interface IEventController
    {

        void Register(object service);

        void Unregister(object service);

    }

    public interface IMultiEventCollection
    {

        IEnumerable<IEventSubscriber> GetSubscribers();

        IEnumerable<IEventPublisher> GetPublishers();

    }

    public class EventController: IEventController
    {

        private List<IEventSubscriber> subscribers = new List<IEventSubscriber>();
        private List<IEventPublisher> publishers = new List<IEventPublisher>();

        public void Register(object service)
        {
            var publisher = service as IEventPublisher;
            if (publisher is not null)
            {
                publisher.OnEvent += Publisher_OnEvent;
                publishers.Add(publisher);
            }
            var subscriber = service as IEventSubscriber;
            if (subscriber is not null)
                subscribers.Add(subscriber);
            var collection = service as IMultiEventCollection;
            if (collection is not null)
                foreach (var item in collection
                    .GetPublishers()
                    .Concat(collection
                        .GetSubscribers()
                        .Cast<object>())
                    .Distinct())
                    Register(item);
        }

        private void Publisher_OnEvent(object sender, NotificationEventArgs e)
        {
            foreach (var subscriber in subscribers)
                try
                {
                    subscriber.ProcessNotification(sender, e);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{ex}");
                }
        }

        public void Unregister(object service)
        {
            var publisher = service as IEventPublisher;
            var existingPublisher = publishers.FirstOrDefault(p => p == publisher);
            if (existingPublisher is not null)
            {
                existingPublisher.OnEvent -= Publisher_OnEvent;
                publishers.Remove(existingPublisher);
            }

            var subscriber = service as IEventSubscriber;
            var existingSubscriber = subscribers.FirstOrDefault(p => p == subscriber);
            if (existingSubscriber is not null)
                subscribers.Remove(existingSubscriber);
            var collection = service as IMultiEventCollection;
            if (collection is not null)
                foreach (var item in collection
                    .GetPublishers()
                    .Concat(collection
                        .GetSubscribers()
                        .Cast<object>())
                    .Distinct())
                    Unregister(item);
        }

    }
}
