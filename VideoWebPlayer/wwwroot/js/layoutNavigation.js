window.videoWebPlayerLayout = window.videoWebPlayerLayout || {};

window.videoWebPlayerLayout.initSidebar = function () {
    const toggleBtn = document.getElementById("sidebarToggle");
    const sidebar = document.getElementById("sidebar");
    const overlay = document.getElementById("sidebar-overlay");

    if (!toggleBtn || !sidebar || !overlay) {
        return;
    }

    if (toggleBtn.dataset.sidebarInitialized === "true") {
        return;
    }

    const closeSidebar = () => {
        sidebar.classList.remove("sidebar-open");
        sidebar.classList.add("sidebar-closed");
        overlay.style.display = "none";
        toggleBtn.setAttribute("aria-expanded", "false");
    };

    toggleBtn.addEventListener("click", () => {
        const willOpen = !sidebar.classList.contains("sidebar-open");
        sidebar.classList.toggle("sidebar-open", willOpen);
        sidebar.classList.toggle("sidebar-closed", !willOpen);
        overlay.style.display = willOpen ? "block" : "none";
        toggleBtn.setAttribute("aria-expanded", willOpen ? "true" : "false");
    });

    overlay.addEventListener("click", closeSidebar);
    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape") {
            closeSidebar();
        }
    });

    toggleBtn.dataset.sidebarInitialized = "true";
    toggleBtn.setAttribute("aria-expanded", sidebar.classList.contains("sidebar-open") ? "true" : "false");
};

document.addEventListener("DOMContentLoaded", window.videoWebPlayerLayout.initSidebar);
document.addEventListener("enhancedload", window.videoWebPlayerLayout.initSidebar);
