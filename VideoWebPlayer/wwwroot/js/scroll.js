window.observeBottom = (element, dotNetHelper) => {
    if (!element || !(element instanceof Element)) {
        console.warn("observeBottom: parameter is not a DOM Element", element);
        return;
    }
    let observer = new IntersectionObserver((entries) => {
        if (entries[0].isIntersecting) {
            dotNetHelper.invokeMethodAsync('OnBottomVisible');
        }
    }, { threshold: 1.0 });
    observer.observe(element);
};