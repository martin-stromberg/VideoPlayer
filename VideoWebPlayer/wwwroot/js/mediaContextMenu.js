window.mediaContextMenu = window.mediaContextMenu || {};

window.mediaContextMenu.position = function (shellElement, menuElement) {
    if (!shellElement || !menuElement) {
        return;
    }

    const margin = 8;
    const shellRect = shellElement.getBoundingClientRect();
    const menuRect = menuElement.getBoundingClientRect();
    const viewportWidth = document.documentElement.clientWidth;
    const viewportHeight = document.documentElement.clientHeight;

    let top = shellRect.bottom - margin - menuRect.height;
    const fitsBelow = top >= margin && shellRect.bottom + margin <= viewportHeight;
    if (!fitsBelow) {
        const above = shellRect.top + margin - menuRect.height;
        if (above >= margin) {
            top = above;
        }
    }
    top = Math.max(margin, Math.min(top, viewportHeight - menuRect.height - margin));

    let left = shellRect.right - margin - menuRect.width;
    left = Math.max(margin, Math.min(left, viewportWidth - menuRect.width - margin));

    menuElement.style.position = "fixed";
    menuElement.style.top = `${top}px`;
    menuElement.style.left = `${left}px`;
    menuElement.style.right = "auto";
    menuElement.style.bottom = "auto";
    menuElement.style.opacity = "1";
};
