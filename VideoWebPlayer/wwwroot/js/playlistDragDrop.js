// Ensures every drag started on a playlist entry row (see PlaylistEntriesList.razor's draggable
// .playlist-entry-row elements) carries at least one dataTransfer.setData(...) call. Firefox refuses to
// start a drag operation at all without this; Chrome/Safari already work without it, but this keeps the
// drag & drop reorder feature cross-browser. Registered as a plain, delegated document-level listener
// (rather than JS interop invoked from PlaylistEntriesList.razor's OnEntryDragStart) because
// dataTransfer.setData(...) can only be called synchronously while the native dragstart event is being
// dispatched, and Blazor Server's C# event handlers run after an async SignalR round-trip - too late for
// the browser to still accept the call.
document.addEventListener("dragstart", (event) => {
    const row = event.target.closest && event.target.closest(".playlist-entry-row");
    if (row && event.dataTransfer) {
        event.dataTransfer.setData("text/plain", row.getAttribute("data-media-id") || "");
    }
});
