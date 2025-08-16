window.enableHorizontalWheelScroll = function (selector) {
    const el = document.querySelector(selector);
    if (!el) return;
    el.addEventListener('wheel', function (e) {
        if (e.deltaY === 0) return;
        e.preventDefault();
        el.scrollLeft += e.deltaY;
    }, { passive: false });
};