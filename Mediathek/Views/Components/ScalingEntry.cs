using System;
using System.Linq;

namespace Mediathek.Views.Components
{
    internal class ScalingEntry: ContentView
    {

        private Entry entry;

        public ScalingEntry()
        {
            // Create the internal Entry
            entry = new Entry();

            // Bind the Text property to the internal Entry
            entry.SetBinding(Entry.TextProperty, new Binding(nameof(Text), source: this));

            // Add the internal Entry to the ContentView's content
            Content = entry;
        }

        public static readonly BindableProperty TextProperty = BindableProperty.Create(nameof(Text),
                                                                                       typeof(string),
                                                                                       typeof(ScalingEntry),
                                                                                       default(string));

        public string Text
        {
            get
            {
                return (string)GetValue(TextProperty);
            }
            set
            {
                SetValue(TextProperty, value);
            }
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);

            // Calculate the desired font size based on the height of the ScalingEntry
            double fontSize = height * 0.4; // Adjust this multiplier as needed for your specific design

            // Set the font size of the internal Entry
            entry.FontSize = fontSize;
        }

    }
}
