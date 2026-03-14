(() => {
    const speedPxPerSec = 120;
    const extraExitPx = 32;

    let nextAvailableTime = 0;

    function getElements() {
        const container = document.getElementById("status-ticker");
        const track = document.getElementById("status-ticker-track");
        if (!container || !track)
            return null;
        return { container, track };
    }

    function enqueue(message) {
        const els = getElements();
        if (!els)
            return;

        const { container, track } = els;

        const item = document.createElement("span");
        item.className = "status-ticker-item";
        item.textContent = String(message ?? "");

        // Append first so we can measure its width.
        track.appendChild(item);

        const containerWidth = container.getBoundingClientRect().width;
        const itemWidth = item.getBoundingClientRect().width;

        item.style.left = `${containerWidth}px`;
        item.style.transform = "translateX(0)";

        const now = performance.now();
        const startTime = Math.max(now, nextAvailableTime);
        const delayMs = startTime - now;

        // Next message may start when the end of this message has become visible.
        // With left starting at containerWidth, this happens after moving itemWidth pixels.
        nextAvailableTime = startTime + (itemWidth / speedPxPerSec) * 1000;

        const distance = containerWidth + itemWidth + extraExitPx;
        const durationMs = (distance / speedPxPerSec) * 1000;

        window.setTimeout(() => {
            const anim = item.animate(
                [
                    { transform: "translateX(0)" },
                    { transform: `translateX(-${distance}px)` }
                ],
                {
                    duration: durationMs,
                    easing: "linear",
                    fill: "forwards"
                });

            anim.onfinish = () => {
                item.remove();
            };
        }, delayMs);
    }

    window.statusTicker = {
        enqueue
    };
})();
