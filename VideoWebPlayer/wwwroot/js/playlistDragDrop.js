// Umordnen der Playlist-Eintraege per Ziehen (PlaylistEntriesList.razor, manueller Sortiermodus).
//
// Bewusst NICHT ueber natives HTML5-Drag & Drop (draggable + dragstart/dragover/drop), sondern ueber
// Pointer-Ereignisse:
//
//  * Natives Drag & Drop verteilte den Ablauf auf zwei getrennte Server-Roundtrips: @ondragstart merkte
//    sich den gezogenen Eintrag auf dem Server, @ondrop wertete ihn beim Loslassen aus. Erreicht die
//    dragstart-Nachricht den Server nicht rechtzeitig (langsame oder unzuverlaessige Verbindung, SignalR
//    Long-Polling ohne garantierte Reihenfolge, kurzzeitiger Verbindungsabbruch), ist der gemerkte Eintrag
//    beim Drop noch null und der Drop wurde still verworfen. Nachgestellt, indem gezielt nur die
//    dragstart-Nachricht verzoegert wurde; ob der Kunde genau das erlebt hat, ist NICHT belegt. Hier laeuft
//    die gesamte Geste im Browser ab; der Server wird genau einmal gerufen, beim Loslassen, mit beiden Ids.
//  * Natives Drag & Drop wirkte nur beim Loslassen exakt ueber einer Kachel. Die Kacheln stehen in einem
//    mehrspaltigen Raster mit Luecken; ein Loslassen dazwischen tat wortlos nichts. Hier wird beim
//    Loslassen neben einer Kachel die naechstliegende genommen (findDropTarget), und ein Loslassen weit
//    ausserhalb sagt das ausdruecklich (showHint) statt wortlos nichts zu tun.
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
    const hintElementId = "playlist-reorder-hint";

    // Ab dieser Strecke (in px) gilt eine Mausbewegung als Ziehen und nicht mehr als Klick.
    const mouseDragThreshold = 6;
    // Bewegt sich ein Finger vor Ablauf der Haltezeit weiter als das, war es eine Scroll-Geste.
    const touchMoveTolerance = 12;
    // So lange muss ein Finger stillhalten, bis das Ziehen beginnt (sonst bleibt Scrollen moeglich).
    const touchHoldMilliseconds = 400;
    // So weit darf der Zeiger beim Loslassen neben der Liste bzw. neben einer Kachel stehen, damit die
    // naechstliegende Kachel noch als Ziel gilt (Luecken des Rasters, Rand der Liste, und am Ende der
    // Liste der Bereich unterhalb der letzten Kachel - dort steht der Finger, wenn er zum Weiterscrollen
    // am unteren Fensterrand gehalten wurde). Als Mass dient die Hoehe einer Kachel: was naeher als eine
    // Kachel an der Liste liegt, ist erkennbar gemeint; alles weiter weg gilt als "danebengelassen" und
    // ordnet nichts um.
    const minimumDropTolerance = 80;
    const maximumDropTolerance = 340;
    // Abstand zum Fensterrand, ab dem waehrend des Ziehens automatisch weitergescrollt wird. Mit dem
    // Finger ist der Streifen breiter: auf Handy-Groesse ist eine Kachel hoeher als ein halber Bildschirm,
    // die Nachbarkachel liegt also ausserhalb des Sichtbereichs und muss herangescrollt werden.
    const autoScrollMarginMouse = 56;
    const autoScrollMarginTouch = 120;
    // Scrollgeschwindigkeit in px/s: am Rand des Streifens langsam, direkt am Fensterrand schnell.
    // Bewusst in px/s und nicht in "px je Takt": wie oft ein setInterval wirklich laeuft, haengt vom
    // Browser ab (gemessen: unter Last statt alle 16 ms nur alle ~90 ms) - mit einer festen Schrittweite
    // je Takt waere die Geschwindigkeit dann um ein Vielfaches zu niedrig, und auf Handy-Groesse kaeme
    // die Nachbarkachel nicht rechtzeitig heran.
    const autoScrollMinimumSpeed = 400;
    const autoScrollMaximumSpeed = 2000;
    const autoScrollIntervalMilliseconds = 16;
    // So lange nach einem Ziehen (oder einem Abbruch) wird der naechste Klick verschluckt, falls er
    // ueberhaupt kommt; nach dem ersten Klick endet die Unterdrueckung sofort.
    const clickSuppressionMilliseconds = 2000;
    const hintVisibleMilliseconds = 5000;

    let listElement = null;
    let dotNetReference = null;
    let gesture = null;
    let autoScrollTimer = null;
    let autoScrollSpeed = 0;
    let autoScrollLastTick = 0;
    let suppressClickUntil = 0;
    let dragFinishedAt = 0;
    let hintTimer = null;
    // Solange ein Umordnen beim Server laeuft, wird keine neue Geste begonnen: sonst liefe die zweite
    // Geste gegen die noch nicht aktualisierte Liste und koennte eine veraltete Zielposition senden.
    let reorderInFlight = false;
    let reorderAttempt = 0;
    // Ueber window.playlistEntryReorder.reorderTimeoutMilliseconds ueberschreibbar (nur fuer Tests).
    const defaultReorderTimeoutMilliseconds = 15000;

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
        reorderInFlight = false;
        reorderAttempt++;

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
        if (!listElement || gesture || reorderInFlight)
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
            overSource: false,
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
        const wasOverSource = gesture.overSource;
        cancelGesture();

        if (!wasDragging)
            return;

        if (!targetRow) {
            // Frueher blieb genau dieser Fall wortlos: der Anwender sieht dann ein Ziehen, das nichts
            // bewirkt. Auf der eigenen Kachel losgelassen ist dagegen erkennbar ein "doch nicht".
            if (!wasOverSource)
                showHint("Nicht umsortiert: Lassen Sie den Titel auf der Kachel der gewünschten Position los.", false);
            return;
        }

        if (!dotNetReference)
            return;

        const sourceId = Number(sourceRow.getAttribute("data-entry-id"));
        const targetId = Number(targetRow.getAttribute("data-entry-id"));
        if (!Number.isFinite(sourceId) || !Number.isFinite(targetId) || sourceId === targetId)
            return;

        sendReorder(sourceId, targetId);
    }

    /// Meldet das Umordnen an die Komponente. Der Aufruf liefert eine Zusage; wird sie abgelehnt (Circuit
    /// beendet, Seitenwechsel waehrend des Ziehens, verworfene Objektreferenz), darf das nicht wortlos
    /// bleiben - sonst waere genau die Wirkung zurueck, die diese Umstellung beseitigen soll.
    function sendReorder(sourceId, targetId) {
        reorderInFlight = true;
        // Bleibt die Zusage schwebend (Senden schlaegt fehl, der Circuit endet mitten im Aufruf), kaeme weder
        // Erfolg noch Ablehnung: ohne Zeitgrenze bliebe reorderInFlight gesetzt und jedes weitere Ziehen im
        // Tab wuerde wortlos blockiert. Ein spaeter eintreffendes Ergebnis wird ueber den Zaehler erkannt.
        const attempt = ++reorderAttempt;
        const timeout = setTimeout(() => {
            if (attempt !== reorderAttempt)
                return;
            reorderInFlight = false;
            console.warn("Der Server hat das Umordnen nicht rechtzeitig bestaetigt.");
            showHint("Der Server hat das Umordnen nicht bestätigt. Bitte prüfen Sie die Reihenfolge, gegebenenfalls laden Sie die Seite neu.", true);
        }, window.playlistEntryReorder.reorderTimeoutMilliseconds || defaultReorderTimeoutMilliseconds);

        const finish = () => {
            clearTimeout(timeout);
            if (attempt === reorderAttempt)
                reorderInFlight = false;
        };

        let promise;
        try {
            promise = dotNetReference.invokeMethodAsync("ReorderEntryByDropAsync", sourceId, targetId);
        } catch (error) {
            finish();
            reportReorderFailure(error);
            return;
        }

        Promise.resolve(promise).then(finish, (error) => {
            finish();
            reportReorderFailure(error);
        });
    }

    function reportReorderFailure(error) {
        console.warn("Das Umordnen konnte nicht an den Server gemeldet werden.", error);
        showHint("Die neue Reihenfolge konnte nicht gespeichert werden. Bitte laden Sie die Seite neu.", true);
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
        if (Date.now() >= suppressClickUntil)
            return;

        // Genau ein Klick wird verschluckt: der, den der Browser nach dem Loslassen ausloest.
        suppressClickUntil = 0;
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

    /// Bestimmt die Kachel, auf der der Titel landen wuerde: die Kachel unter dem Zeiger, und wenn dort
    /// keine ist (Luecke im Raster, Rand der Liste), die naechstgelegene im Umfeld der Liste. Steht der
    /// Zeiger weit ausserhalb, gibt es kein Ziel.
    function findDropTarget(x, y) {
        const elementUnderPointer = document.elementFromPoint(x, y);
        const direct = elementUnderPointer && elementUnderPointer.closest
            ? elementUnderPointer.closest(rowSelector)
            : null;
        if (direct && listElement.contains(direct))
            return direct;

        const tolerance = dropTolerance();
        const listRectangle = listElement.getBoundingClientRect();
        if (x < listRectangle.left - tolerance || x > listRectangle.right + tolerance
            || y < listRectangle.top - tolerance || y > listRectangle.bottom + tolerance)
            return null;

        let nearest = null;
        let nearestDistance = Number.POSITIVE_INFINITY;
        const rows = listElement.querySelectorAll(rowSelector);
        for (let index = 0; index < rows.length; index++) {
            const row = rows[index];
            // Die gezogene Kachel selbst zaehlt hier nicht mit, sonst waere die Luecke direkt neben ihr
            // wieder eine tote Zone.
            if (gesture && row === gesture.row)
                continue;

            const rectangle = row.getBoundingClientRect();
            const horizontal = Math.max(rectangle.left - x, 0, x - rectangle.right);
            const vertical = Math.max(rectangle.top - y, 0, y - rectangle.bottom);
            const distance = Math.hypot(horizontal, vertical);
            if (distance < nearestDistance) {
                nearestDistance = distance;
                nearest = row;
            }
        }

        return nearestDistance <= tolerance ? nearest : null;
    }

    /// Die Kachelhoehe als Mass dafuer, was noch "neben der Kachel" und was schon "weit daneben" ist -
    /// auf dem Handy ist eine Kachel rund ein Drittel des Bildschirms hoch, auf dem Schreibtisch deutlich
    /// flacher, ein fester Wert waere fuer das eine zu klein und fuer das andere zu grosszuegig.
    function dropTolerance() {
        const height = gesture && gesture.row ? gesture.row.getBoundingClientRect().height : 0;
        return Math.min(Math.max(height, minimumDropTolerance), maximumDropTolerance);
    }

    /// Markiert die Zielkachel (mit Einfuegelinie vor oder hinter der Kachel, je nachdem, ob der Titel
    /// nach vorn oder nach hinten wandert). Die Markierung wandert mit dem Zeiger, auch ueber Luecken.
    function updateDropTarget() {
        if (!gesture || !gesture.dragging || !listElement)
            return;

        const candidate = findDropTarget(gesture.lastX, gesture.lastY);
        gesture.overSource = candidate === gesture.row;
        const target = candidate && candidate !== gesture.row ? candidate : null;

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
    /// (sonst waeren nur die gerade sichtbaren Kacheln als Ziel erreichbar - auf Handy-Groesse ist das
    /// nicht einmal die Nachbarkachel).
    function updateAutoScroll() {
        if (!gesture || !gesture.dragging) {
            stopAutoScroll();
            return;
        }

        const margin = gesture.pointerType === "mouse" ? autoScrollMarginMouse : autoScrollMarginTouch;
        const distanceToTop = gesture.lastY;
        const distanceToBottom = window.innerHeight - gesture.lastY;
        const direction = distanceToTop < margin ? -1 : (distanceToBottom < margin ? 1 : 0);

        if (direction === 0) {
            stopAutoScroll();
            return;
        }

        // Je naeher am Fensterrand, desto schneller.
        const distance = direction < 0 ? distanceToTop : distanceToBottom;
        const depth = Math.min(Math.max((margin - distance) / margin, 0), 1);
        autoScrollSpeed = direction * (autoScrollMinimumSpeed + (autoScrollMaximumSpeed - autoScrollMinimumSpeed) * depth);

        if (autoScrollTimer)
            return;

        autoScrollLastTick = Date.now();
        autoScrollTimer = window.setInterval(() => {
            if (!gesture || !gesture.dragging || autoScrollSpeed === 0) {
                stopAutoScroll();
                return;
            }

            const now = Date.now();
            const elapsedSeconds = Math.min((now - autoScrollLastTick) / 1000, 0.25);
            autoScrollLastTick = now;
            scrollWindowBy(autoScrollSpeed * elapsedSeconds);
            updateDropTarget();
        }, autoScrollIntervalMilliseconds);
    }

    /// Scrollt sofort um den gegebenen Betrag. Bootstrap setzt auf :root ein "scroll-behavior: smooth"
    /// (ausser bei "prefers-reduced-motion"); ein gewoehnliches window.scrollBy waere damit eine
    /// Animation, die der naechste Takt sofort wieder abbricht - gemessen blieben von den angeforderten
    /// rund 1700 px/s nur etwa 150 px/s uebrig, und auf Handy-Groesse kam die Nachbarkachel nie heran.
    function scrollWindowBy(amount) {
        try {
            window.scrollTo({ top: window.scrollY + amount, left: window.scrollX, behavior: "instant" });
        } catch (error) {
            // Aeltere Browser kennen "instant" nicht.
            window.scrollBy(0, amount);
        }
    }

    function stopAutoScroll() {
        autoScrollSpeed = 0;
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

    /// Beendet die laufende Geste und raeumt Markierungen, Pointer Capture und Timer auf. War ein Ziehen
    /// im Gange (auch bei Abbruch mit Escape), wird der anschliessende Klick des Browsers verschluckt:
    /// ein Ziehen - und erst recht ein abgebrochenes - darf keinen Titel auswaehlen.
    function cancelGesture() {
        stopAutoScroll();
        if (!gesture)
            return;

        if (gesture.dragging) {
            suppressClickUntil = Date.now() + clickSuppressionMilliseconds;
            dragFinishedAt = Date.now();
        }

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

    /// Zeigt eine kurze Rueckmeldung am unteren Bildschirmrand. Bewusst direkt am <body> und nicht
    /// innerhalb der von Blazor gerenderten Liste (fremde Knoten dort wuerden dessen Abgleich stoeren),
    /// und bewusst ohne Server: die Meldung muss auch dann erscheinen, wenn die Verbindung gerade weg ist.
    function showHint(message, isError) {
        let hint = document.getElementById(hintElementId);
        if (!hint) {
            hint = document.createElement("div");
            hint.id = hintElementId;
            hint.className = "playlist-reorder-hint";
            hint.setAttribute("role", "status");
            document.body.appendChild(hint);
        }

        hint.textContent = message;
        hint.classList.toggle("playlist-reorder-hint-error", !!isError);
        hint.classList.add("playlist-reorder-hint-visible");

        if (hintTimer)
            window.clearTimeout(hintTimer);
        hintTimer = window.setTimeout(() => {
            hint.classList.remove("playlist-reorder-hint-visible");
            hintTimer = null;
        }, hintVisibleMilliseconds);
    }

    window.playlistEntryReorder = {
        attach: attach,
        detach: detach,
        reorderTimeoutMilliseconds: defaultReorderTimeoutMilliseconds
    };
})();
