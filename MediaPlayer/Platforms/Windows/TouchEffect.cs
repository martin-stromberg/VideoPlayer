using MediaPlayer.Helper.Touch;

namespace MediaPlayer.Platforms.Windows
{

    public class TouchEffect: RoutingEffect
    {

        public event TouchActionEventHandler TouchAction;

        public TouchEffect()
            : base("XamarinDocs.TouchEffect") { }

        public bool Capture { set; get; }

        public void OnTouchAction(Element element, TouchActionEventArgs args)
        {
            TouchAction?.Invoke(element, args);
        }

    }
}
