namespace VideoPlayer.ViewModels
{
    public interface IViewModelAppearance
    {

        /// <summary>
        /// Wird aufgerufen, wenn die View mit diesem Viewmodel angezeigt wird.
        /// </summary>
        void OnAppeared();

        /// <summary>
        /// Wird aufgerufen, wenn die View mit dem Viewmodel ausgeblendet wird.
        /// </summary>
        /// <param name="closing">
        /// Gibt an, ob die Ansicht geschlossen wird (true), oder nur in den Hintergrund gelegt wird, weil eine neue Ansicht
        /// geöffnet wird. (false)
        /// </param>
        void OnDisappeared(bool closing);

    }
}
