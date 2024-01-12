using System;
using System.Linq;

namespace Mediathek.Helper.Touch
{
    internal class TouchRoutingEffect: RoutingEffect
    {

        public event TouchActionEventHandler TouchAction;

        public TouchRoutingEffect() { }

        public bool Capture { set; get; }

        public void OnTouchAction(Element element, TouchActionEventArgs args)
        {
            TouchAction?.Invoke(element, args);
        }

    }
}
