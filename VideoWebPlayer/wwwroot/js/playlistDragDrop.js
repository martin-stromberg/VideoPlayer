// Umordnen der Playlist-Eintraege per Ziehen (PlaylistEntriesList.razor, manueller Sortiermodus).
//
// Bewusst NICHT ueber natives HTML5-Drag & Drop (draggable + dragstart/dragover/drop), sondern ueber
// Pointer-Ereignisse:
//
//  * Natives Drag & Drop haette den Ablauf auf zwei getrennte Server-Roundtrips verteilt: @ondragstart
//    merkt sich den gezogenen Eintrag auf dem Server, @ondrop wertet ihn beim Loslassen aus. Erreicht die
//    dragstart-Nachricht den Server nicht rechtzeitig (langsame oder unzuverlaessige Verbindung, SignalR
//    Long-Polling ohne garantierte Reihenfolge, kurzzeitiger Verbindungsabbruch), ist der gemerkte Eintrag
//    beim Drop noch null und der Drop wird still verworfen - genau das vom Kunden gemeldete Verhalten
//    ("Ziehen bewirkt nichts, nur die Schaltflaechen funktionieren"), ohne Fehlermeldung. Hier laeuft die
//    gesamte Geste im Browser ab; der Server wird genau einmal gerufen, beim Loslassen, mit beiden Ids.
//  * Natives Drag & Drop funktioniert auf Touch-Geraeten grundsaetzlich nicht. Pointer-Ereignisse decken
//    Maus, Stift und Touch (Ziehen nach kurzem Halten) mit demselben Code ab.
//  * Waehrend des Ziehens ist eine Rueckmeldung noetig (gedaempfte Quelle, markiertes Ziel). Mit nativem
//    Drag & Drop haette jede Markierung einen Server-Roundtrip gekostet.
//
// Die Semantik bleibt unveraendert: Loslassen ueber Kachel X verschiebt den gezogenen Titel auf die
// Position von X (serverseitig MoveEntryBetweenAsync mit NewSortOrder = SortOrder von X).
(function () {
    "use strict";

    // Nur Kacheln mit der Markierung aus PlaylistEntriesList.razor (Besitzer, manueller Modus).
    const rowSelector = ".playlist-entry-row.playlist-entry-reorderable";
    const interactiveSelector = "button, a, input, select, textarea";

    // Ab dieser Strecke (in px) gilt eine Mausbewegung als Ziehen und nicht mehr als Klick.
    const mouseDragThreshold = 6;
    // Bewegt sich ein Finger vor Ablauf der Haltezeit weiter als das, war es eine Scroll-Geste.
    const touchMoveTolerance = 12;
    // So lange muss ein Finger stillhalten, bis das Ziehen beginnt (sonst bleibt Scrollen moeglich).
    const touchHoldMilliseconds = 400;
    // Abstand zum Fensterrand, ab dem waehrend des Ziehens automatisch weitergescrollt wird. Bewusst
    // schmal: ein zu breiter Streifen wuerde schon beim Ziehen auf eine tief stehende Kachel losscrollen
    // und dem Zeiger das Ziel unter dem Finger wegziehen.
    const autoScrollMargin = 40;
    const autoScrollMaximumStep = 12;
    const autoScrollIntervalMilliseconds = 16;

    let listElement = null;
    let dotNetReference = null;
    let gesture = null;
    let autoScrollTimer = null;
    let autoScrollStep = 0;
    let suppressNextClick = false;
    let dragFinishedAt = 0;

    /// Beginnt die Beobachtung einer Eintragsliste. Mehrfaches Aufrufen mit derselben Liste ist
    /// wirkungslos, ein Aufruf mit einer anderen Liste loest die bisherige ab.
    function attach(element, reference) {
        if (!element)
            return;

        if (listElement === element) {
            dotNetReference = reference;
            return;
        }

        detach();
        listElement = element;
        dotNetReference = reference;

        listElement.addEventListener("pointerdown", onPointerDown);
        // Nicht passiv, damit waehrend des Ziehens das Scrollen per Finger unterdrueckt werden kann.
        listElement.addEventListener("touchmove", onTouchMove, { passive: false });
        // Bilder und Text sind von Haus aus nativ ziehbar; ein solcher Drag wuerde die Geste abbrechen.
        listElement.addEventListener("dragstart", onNativeDragStart);
        window.addEventListener("pointermove", onPointerMove);
        window.addEventListener("pointerup", onPointerUp);
        window.addEventListener("pointercancel", onPointerCancel);
        window.addEventListener("keydown", onKeyDown);
        document.addEventListener("click", onClickCapture, true);
        document.addEventListener("dblclick", onDoubleClickCapture, true);
    }

    /// Beendet die Beobachtung (anderer Bereich, Datumsmodus, fremde Playlist, Verlassen der Seite).
    function detach() {
        cancelGesture();

        if (!listElement)
            return;

        listElement.removeEventListener("pointerdown", onPointerDown);
        listElement.removeEventListener("touchmove", onTouchMove);
        listElement.removeEventListener("dragstart", onNativeDragStart);
        window.removeEventListener("pointermove", onPointerMove);
        window.removeEventListener("pointerup", onPointerUp);
        window.removeEventListener("pointercancel", onPointerCancel);
        window.removeEventListener("keydown", onKeyDown);
        document.removeEventListener("click", onClickCapture, true);
        document.removeEventListener("dblclick", onDoubleClickCapture, true);

        listElement = null;
        dotNetReference = null;
    }

    function onNativeDragStart(event) {
        event.preventDefault();
    }

    function onPointerDown(event) {
        if (!listElement || gesture)
            return;
        if (!event.isPrimary)
            return;
        if (event.pointerType === "mouse" && event.button !== 0)
            return;

        const target = event.target;
        if (!target || !target.closest)
            return;
        // Die Schnellaktionen "An Anfang"/"An Ende" bleiben gewoehnliche Schaltflaechen.
        if (target.closest(interactiveSelector))
            return;

        const row = target.closest(rowSelector);
        if (!row || !listElement.contains(row))
            return;

        gesture = {
            pointerId: event.pointerId,
            pointerType: event.pointerType,
            row: row,
            startX: event.clientX,
            startY: event.clientY,
            lastX: event.clientX,
            lastY: event.clientY,
            dragging: false,
            target: null,
            holdTimer: null
        };

        if (event.pointerType !== "mouse") {
            gesture.holdTimer = window.setTimeout(() => {
                if (gesture && !gesture.dragging)
                    beginDrag();
            }, touchHoldMilliseconds);
        }
    }

    function onPointerMove(event) {
        if (!gesture || event.pointerId !== gesture.pointerId)
            return;

        gesture.lastX = event.clientX;
        gesture.lastY = event.clientY;

        if (!gesture.dragging) {
            const distance = Math.hypot(event.clientX - gesture.startX, event.clientY - gesture.startY);
            if (gesture.pointerType === "mouse") {
                if (distance < mouseDragThreshold)
                    return;
                beginDrag();
            } else {
                // Finger/Stift: Bewegung vor Ablauf der Haltezeit ist Scrollen, kein Ziehen.
                if (distance > touchMoveTolerance)
                    cancelGesture();
                return;
            }
        }

        updateDropTarget();
        updateAutoScroll();
    }

    function onTouchMove(event) {
        // Waehrend des Ziehens darf die Seite nicht unter dem Finger wegscrollen.
        if (gesture && gesture.dragging && event.cancelable)
            event.preventDefault();
    }

    function onPointerUp(event) {
        if (!gesture || event.pointerId !== gesture.pointerId)
            return;

        const wasDragging = gesture.dragging;
        const sourceRow = gesture.row;
        const targetRow = gesture.target;
        cancelGesture();

        if (!wasDragging)
            return;

        // Ein Ziehen darf weder eine Auswahl noch (per Doppelklick) das Abspielen ausloesen.
        suppressNextClick = true;
        dragFinishedAt = Date.now();
        window.setTimeout(() => { suppressNextClick = false; }, 0);

        if (!targetRow || !dotNetReference)
            return;

        const sourceId = Number(sourceRow.getAttribute("data-entry-id"));
        const targetId = Number(targetRow.getAttribute("data-entry-id"));
        if (!Number.isFinite(sourceId) || !Number.isFinite(targetId) || sourceId === targetId)
            return;

        try {
            dotNetReference.invokeMethodAsync("ReorderEntryByDropAsync", sourceId, targetId);
        } catch (error) {
            // Die Komponente ist bereits verschwunden (Seitenwechsel waehrend des Ziehens).
        }
    }

    function onPointerCancel(event) {
        if (gesture && event.pointerId === gesture.pointerId)
            cancelGesture();
    }

    function onKeyDown(event) {
        if (event.key === "Escape" && gesture)
            cancelGesture();
    }

    function onClickCapture(event) {
        if (!suppressNextClick)
            return;

        suppressNextClick = false;
        event.stopPropagation();
        event.preventDefault();
    }

    function onDoubleClickCapture(event) {
        // Zwei schnell aufeinanderfolgende Zieh-Gesten duerfen keinen Doppelklick ergeben.
        if (Date.now() - dragFinishedAt > 400)
            return;

        event.stopPropagation();
        event.preventDefault();
    }

    function beginDrag() {
        if (!gesture || gesture.dragging)
            return;

        gesture.dragging = true;
        clearHoldTimer();
        try {
            gesture.row.setPointerCapture(gesture.pointerId);
        } catch (error) {
            // Pointer Capture ist nicht zwingend; ohne sie liefern die Fenster-Listener die Ereignisse.
        }

        gesture.row.classList.add("playlist-entry-dragging");
        if (listElement)
            listElement.classList.add("playlist-entries-reordering");

        updateDropTarget();
    }

    /// Bestimmt die Kachel unter dem Zeiger und markiert sie als Ziel (mit Einfuegelinie vor oder hinter
    /// der Kachel, je nachdem, ob der Titel nach vorn oder nach hinten wandert).
    function updateDropTarget() {
        if (!gesture || !gesture.dragging || !listElement)
            return;

        const elementUnderPointer = document.elementFromPoint(gesture.lastX, gesture.lastY);
        const candidate = elementUnderPointer && elementUnderPointer.closest
            ? elementUnderPointer.closest(rowSelector)
            : null;
        const target = candidate && listElement.contains(candidate) && candidate !== gesture.row
            ? candidate
            : null;

        if (target === gesture.target)
            return;

        clearTargetMarker();
        gesture.target = target;
        if (!target)
            return;

        const rows = Array.prototype.slice.call(listElement.querySelectorAll(rowSelector));
        const movesBackwards = rows.indexOf(target) > rows.indexOf(gesture.row);
        target.classList.add("playlist-entry-drop-target");
        target.classList.add(movesBackwards ? "playlist-entry-drop-after" : "playlist-entry-drop-before");
    }

    /// Scrollt die Seite weiter, solange der Zeiger waehrend des Ziehens am oberen oder unteren Rand steht
    /// (sonst waeren nur die gerade sichtbaren Kacheln als Ziel erreichbar).
    function updateAutoScroll() {
        if (!gesture || !gesture.dragging) {
            stopAutoScroll();
            return;
        }

        const distanceToTop = gesture.lastY;
        const distanceToBottom = window.innerHeight - gesture.lastY;
        // Je naeher am Rand, desto schneller - direkt am Rand mit voller Schrittweite.
        const step = distanceToTop < autoScrollMargin
            ? -Math.ceil(autoScrollMaximumStep * (autoScrollMargin - distanceToTop) / autoScrollMargin)
            : (distanceToBottom < autoScrollMargin
                ? Math.ceil(autoScrollMaximumStep * (autoScrollMargin - distanceToBottom) / autoScrollMargin)
                : 0);

        autoScrollStep = step;
        if (step === 0) {
            stopAutoScroll();
            return;
        }

        if (autoScrollTimer)
            return;

        autoScrollTimer = window.setInterval(() => {
            if (!gesture || !gesture.dragging || autoScrollStep === 0) {
                stopAutoScroll();
                return;
            }

            window.scrollBy(0, autoScrollStep);
            updateDropTarget();
        }, autoScrollIntervalMilliseconds);
    }

    function stopAutoScroll() {
        autoScrollStep = 0;
        if (!autoScrollTimer)
            return;

        window.clearInterval(autoScrollTimer);
        autoScrollTimer = null;
    }

    function clearTargetMarker() {
        if (!gesture || !gesture.target)
            return;

        gesture.target.classList.remove("playlist-entry-drop-target");
        gesture.target.classList.remove("playlist-entry-drop-before");
        gesture.target.classList.remove("playlist-entry-drop-after");
    }

    function clearHoldTimer() {
        if (!gesture || !gesture.holdTimer)
            return;

        window.clearTimeout(gesture.holdTimer);
        gesture.holdTimer = null;
    }

    /// Beendet die laufende Geste und raeumt Markierungen, Pointer Capture und Timer auf.
    function cancelGesture() {
        stopAutoScroll();
        if (!gesture)
            return;

        clearHoldTimer();
        clearTargetMarker();
        gesture.row.classList.remove("playlist-entry-dragging");
        if (listElement)
            listElement.classList.remove("playlist-entries-reordering");

        try {
            if (gesture.row.hasPointerCapture && gesture.row.hasPointerCapture(gesture.pointerId))
                gesture.row.releasePointerCapture(gesture.pointerId);
        } catch (error) {
            // Der Zeiger ist bereits freigegeben.
        }

        gesture = null;
    }

    window.playlistEntryReorder = {
        attach: attach,
        detach: detach
    };
})();
