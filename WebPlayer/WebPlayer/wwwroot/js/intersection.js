window.intersectionObserver = {
    observeElement: function (element, dotNetHelper) {
        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    dotNetHelper.invokeMethodAsync('OnIntersect');
                }
            });
        }, {
            root: null,
            rootMargin: '0px',
            threshold: 1.0
        });
        if (element != null)
            observer.observe(element);
    },

    unobserveElement: function (element) {
        if (element._observer) {
            element._observer.unobserve(element);
            element._observer.disconnect();
            delete element._observer;
        }
    },

    isElementInViewport: function (element) {
        if (element == null) return false;
        const rect = element.getBoundingClientRect();
        return (
            rect.top < (window.innerHeight || document.documentElement.clientHeight) &&
            rect.bottom > 0
        );
    }
};
